package main

import (
	"crypto/rand"
	"database/sql"
	"encoding/json"
	"flag"
	"fmt"
	"html/template"
	"log"
	"math/big"
	"net/http"
	"os"
	"path/filepath"
	"strings"
	"sync"
	"time"

	"github.com/google/uuid"
	_ "modernc.org/sqlite"
	"golang.org/x/crypto/bcrypt"
)

type Config struct {
	APIKey         string
	StorageBaseDir string
	Port           string
	PublicDomain   string
}

type DownloadPageData struct {
	FileName         string
	FileSize         string
	ExpireTime       string
	Message          string
	RequirePassword  bool
	DownloadCount    int
	MaxDownloadCount int
	ShareType        string
	Error            string
}

var (
	db             *sql.DB
	appConfig      Config
	failedAttempts = make(map[string]int)
	failLock       sync.Mutex
	dataDir        string
)

func formatFileSize(bytes int64) string {
	const unit = 1024
	if bytes < unit {
		return fmt.Sprintf("%d B", bytes)
	}
	div, exp := int64(unit), 0
	for n := bytes / unit; n >= unit; n /= unit {
		div *= unit
		exp++
	}
	return fmt.Sprintf("%.1f %cB", float64(bytes)/float64(div), "KMGTPE"[exp])
}

func initDB() {
	var err error
	dbPath := filepath.Join(dataDir, "share.db")
	
	db, err = sql.Open("sqlite", dbPath)
	if err != nil {
		log.Fatalf("[DB-ERROR] sql.Open 실패: %v", err)
	}

	if err = db.Ping(); err != nil {
		log.Fatalf("[DB-ERROR] db.Ping 실패 (파일 생성 불가): %v", err)
	}

	createTableQuery := `
	CREATE TABLE IF NOT EXISTS shares (
		uuid TEXT PRIMARY KEY,
		share_name TEXT UNIQUE NOT NULL,
		access_token TEXT NOT NULL,
		original_name TEXT NOT NULL,
		storage_name TEXT NOT NULL,
		status TEXT NOT NULL,
		expire_at DATETIME,
		password_hash TEXT,
		created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
		memo TEXT DEFAULT '',
		max_downloads INTEGER DEFAULT 0,
		current_downloads INTEGER DEFAULT 0
	);`
	
	_, err = db.Exec(createTableQuery)
	if err != nil {
		log.Fatalf("[DB-ERROR] 테이블 생성 실패: %v", err)
	}

	_, err = db.Exec("ALTER TABLE shares ADD COLUMN max_downloads INTEGER DEFAULT 0;")
	if err != nil && !strings.Contains(err.Error(), "duplicate column name") {
		log.Printf("[DB-WARN] 컬럼 추가 에러: %v", err)
	}
	
	_, err = db.Exec("ALTER TABLE shares ADD COLUMN current_downloads INTEGER DEFAULT 0;")
	if err != nil && !strings.Contains(err.Error(), "duplicate column name") {
		log.Printf("[DB-WARN] 컬럼 추가 에러: %v", err)
	}
}

func initStorage() {
	tempDir := filepath.Join(appConfig.StorageBaseDir, ".temp")
	os.MkdirAll(tempDir, 0755)
}

func authMiddleware(next http.HandlerFunc) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		clientKey := r.Header.Get("X-API-Key")
		
		if clientKey != appConfig.APIKey {
			http.Error(w, `{"error": "Unauthorized"}`, http.StatusUnauthorized)
			return
		}
		next.ServeHTTP(w, r)
	}
}

func generateBase62Token(length int) string {
	const charset = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789"
	b := make([]byte, length)
	for i := range b {
		n, _ := rand.Int(rand.Reader, big.NewInt(int64(len(charset))))
		b[i] = charset[n.Int64()]
	}
	return string(b)
}

func getInfoHandler(w http.ResponseWriter, r *http.Request) {
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]string{
		"public_domain": appConfig.PublicDomain,
	})
}

type InitShareRequest struct {
	ShareName    string `json:"share_name"`
	OriginalName string `json:"original_name"`
	ExpireAt     string `json:"expire_at"`
	PasswordHash string `json:"password_hash"`
	Memo         string `json:"memo"`
	MaxDownloads int    `json:"max_downloads"`
}

func initShareHandler(w http.ResponseWriter, r *http.Request) {
	var req InitShareRequest
	json.NewDecoder(r.Body).Decode(&req)

	var exists bool
	db.QueryRow("SELECT EXISTS(SELECT 1 FROM shares WHERE share_name = ?)", req.ShareName).Scan(&exists)
	if exists {
		w.WriteHeader(http.StatusConflict)
		w.Write([]byte(`{"error": "Share name already exists"}`))
		return
	}

	newUUID := uuid.New().String()
	token := generateBase62Token(8)

	var expireAt interface{} = req.ExpireAt
	if req.ExpireAt == "" {
		expireAt = nil
	}

	var passwordHash interface{} = nil
	if req.PasswordHash != "" {
		hash, err := bcrypt.GenerateFromPassword([]byte(req.PasswordHash), bcrypt.DefaultCost)
		if err == nil {
			passwordHash = string(hash)
		}
	}

	_, err := db.Exec(`INSERT INTO shares (uuid, share_name, access_token, original_name, storage_name, status, expire_at, password_hash, memo, max_downloads) VALUES (?, ?, ?, ?, ?, 'pending', ?, ?, ?, ?)`,
		newUUID, req.ShareName, token, req.OriginalName, newUUID, expireAt, passwordHash, req.Memo, req.MaxDownloads)

	if err != nil {
		log.Printf("[INIT-ERROR] DB Insert 실패: %v\n", err)
		http.Error(w, fmt.Sprintf(`{"error": "DB Insert failed: %v"}`, err), http.StatusInternalServerError)
		return
	}

	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(map[string]string{"uuid": newUUID, "access_token": token})
}

type UpdateShareRequest struct {
	ExpireAt       string `json:"expire_at"`
	UpdatePassword bool   `json:"update_password"`
	Password       string `json:"password"`
	Memo           string `json:"memo"`
	MaxDownloads   int    `json:"max_downloads"`
}

func updateShareHandler(w http.ResponseWriter, r *http.Request) {
	uuidParam := r.PathValue("uuid")
	var req UpdateShareRequest
	if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
		http.Error(w, `{"error": "Invalid payload"}`, http.StatusBadRequest)
		return
	}

	var expireAt interface{} = req.ExpireAt
	if req.ExpireAt == "" {
		expireAt = nil
	}

	query := "UPDATE shares SET expire_at = ?, memo = ?, max_downloads = ?"
	args := []interface{}{expireAt, req.Memo, req.MaxDownloads}

	if req.UpdatePassword {
		query += ", password_hash = ?"
		if req.Password == "" {
			args = append(args, nil)
		} else {
			hash, err := bcrypt.GenerateFromPassword([]byte(req.Password), bcrypt.DefaultCost)
			if err != nil {
				http.Error(w, `{"error": "Password hash failed"}`, http.StatusInternalServerError)
				return
			}
			args = append(args, string(hash))
		}
	}

	query += " WHERE uuid = ?"
	args = append(args, uuidParam)

	_, err := db.Exec(query, args...)
	if err != nil {
		http.Error(w, `{"error": "DB update failed"}`, http.StatusInternalServerError)
		return
	}

	w.Write([]byte(`{"message": "Share info updated successfully"}`))
}

func completeShareHandler(w http.ResponseWriter, r *http.Request) {
	uuidParam := r.PathValue("uuid")
	var status, storageName string
	err := db.QueryRow("SELECT status, storage_name FROM shares WHERE uuid = ?", uuidParam).Scan(&status, &storageName)
	if err != nil {
		http.Error(w, `{"error": "DB error"}`, http.StatusInternalServerError)
		return
	}

	if status != "pending" {
		http.Error(w, `{"error": "Not pending"}`, http.StatusBadRequest)
		return
	}

	tempFilePath := filepath.Join(appConfig.StorageBaseDir, ".temp", storageName)
	finalFilePath := filepath.Join(appConfig.StorageBaseDir, storageName)

	log.Printf("[COMPLETE-INFO] 파일 이동 시도 중... 출발지: %s, 목적지: %s\n", tempFilePath, finalFilePath)

	if err := os.Rename(tempFilePath, finalFilePath); err != nil {
		log.Printf("[COMPLETE-ERROR] 파일 이동 실패! 이유: %v\n", err)
		http.Error(w, fmt.Sprintf(`{"error": "File move failed: %v"}`, err), http.StatusInternalServerError)
		return
	}

	db.Exec("UPDATE shares SET status = 'active' WHERE uuid = ?", uuidParam)
	log.Printf("[COMPLETE-SUCCESS] %s 업로드 완벽 성공!\n", uuidParam)
	w.Write([]byte(`{"message": "Upload completed successfully", "status": "active"}`))
}

type ShareItem struct {
	UUID             string  `json:"uuid"`
	ShareName        string  `json:"share_name"`
	AccessToken      string  `json:"access_token"`
	OriginalName     string  `json:"original_name"`
	Status           string  `json:"status"`
	ExpireAt         *string `json:"expire_at"`
	CreatedAt        string  `json:"created_at"`
	Memo             string  `json:"memo"`
	MaxDownloads     int     `json:"max_downloads"`
	CurrentDownloads int     `json:"current_downloads"`
	HasPassword      bool    `json:"has_password"`
}

func listSharesHandler(w http.ResponseWriter, r *http.Request) {
	rows, _ := db.Query("SELECT uuid, share_name, access_token, original_name, status, expire_at, created_at, memo, max_downloads, current_downloads, password_hash FROM shares WHERE status != 'deleted'")
	defer rows.Close()

	var shares []ShareItem
	for rows.Next() {
		var item ShareItem
		var memo sql.NullString
		var passHash sql.NullString

		rows.Scan(&item.UUID, &item.ShareName, &item.AccessToken, &item.OriginalName, &item.Status, &item.ExpireAt, &item.CreatedAt, &memo, &item.MaxDownloads, &item.CurrentDownloads, &passHash)

		item.Memo = memo.String
		item.HasPassword = passHash.Valid && passHash.String != ""
		shares = append(shares, item)
	}

	if shares == nil {
		shares = []ShareItem{}
	}
	w.Header().Set("Content-Type", "application/json")
	json.NewEncoder(w).Encode(shares)
}

func updateStatusHandler(w http.ResponseWriter, r *http.Request) {
	uuidParam := r.PathValue("uuid")
	var req struct {
		Status string `json:"status"`
	}
	json.NewDecoder(r.Body).Decode(&req)
	db.Exec("UPDATE shares SET status = ? WHERE uuid = ?", req.Status, uuidParam)
	w.Write([]byte(`{"message": "Status updated"}`))
}

func deleteShareHandler(w http.ResponseWriter, r *http.Request) {
	uuidParam := r.PathValue("uuid")
	db.Exec("UPDATE shares SET status = 'deleted' WHERE uuid = ?", uuidParam)
	w.Write([]byte(`{"message": "Share marked as deleted"}`))
}

func startGarbageCollector() {
	ticker := time.NewTicker(1 * time.Hour)
	go func() {
		for {
			<-ticker.C
			rows, _ := db.Query("SELECT uuid, storage_name FROM shares WHERE status = 'deleted'")
			for rows.Next() {
				var uuid, storageName string
				rows.Scan(&uuid, &storageName)
				os.Remove(filepath.Join(appConfig.StorageBaseDir, storageName))
				db.Exec("DELETE FROM shares WHERE uuid = ?", uuid)
			}
			rows.Close()
		}
	}()
}

func downloadHandler(w http.ResponseWriter, r *http.Request) {
	clientIP := r.Header.Get("X-Real-IP")
	if clientIP == "" {
		clientIP = strings.Split(r.RemoteAddr, ":")[0]
	}

	if strings.HasPrefix(r.URL.Path, "/api/") || strings.HasPrefix(r.URL.Path, "/static/") || r.URL.Path == "/favicon.ico" {
		http.NotFound(w, r)
		return
	}

	path := r.URL.Path[1:]
	lastDash := strings.LastIndex(path, "-")
	if lastDash == -1 {
		http.Error(w, "Invalid download link format", http.StatusBadRequest)
		return
	}
	shareName := path[:lastDash]
	accessToken := path[lastDash+1:]

	var storageName, originalName, status, memo string
	var expireAtStr, passwordHash *string
	var maxDownloads, currentDownloads int

	err := db.QueryRow(`
		SELECT storage_name, original_name, status, expire_at, password_hash, memo, max_downloads, current_downloads 
		FROM shares 
		WHERE share_name = ? AND access_token = ?`,
		shareName, accessToken).Scan(&storageName, &originalName, &status, &expireAtStr, &passwordHash, &memo, &maxDownloads, &currentDownloads)

	if err == sql.ErrNoRows {
		http.Error(w, "File not found or link is broken", http.StatusNotFound)
		return
	}

	isExpired := status != "active" || (maxDownloads > 0 && currentDownloads >= maxDownloads)

	filePath := filepath.Join(appConfig.StorageBaseDir, storageName)
	fileInfo, err := os.Stat(filePath)
	fileSizeStr := "Unknown"
	if err == nil {
		fileSizeStr = formatFileSize(fileInfo.Size())
	}

	expireTimeStr := "무제한"
	if expireAtStr != nil && *expireAtStr != "" {
		if t, err := time.Parse(time.RFC3339, *expireAtStr); err == nil {
			expireTimeStr = t.Format("2006-01-02 15:04")
			if time.Now().After(t) {
				isExpired = true
			}
		}
	}

	if isExpired {
		http.Error(w, "This shared file has expired or reached its download limit.", http.StatusForbidden)
		return
	}

	pageData := DownloadPageData{
		FileName:         originalName,
		FileSize:         fileSizeStr,
		ExpireTime:       expireTimeStr,
		Message:          memo,
		RequirePassword:  passwordHash != nil && *passwordHash != "",
		DownloadCount:    currentDownloads,
		MaxDownloadCount: maxDownloads,
	}

	if pageData.RequirePassword {
		failLock.Lock()
		fails := failedAttempts[clientIP]
		failLock.Unlock()

		if fails >= 5 {
			pageData.Error = "비밀번호 입력 횟수를 초과했습니다. 잠시 후 다시 시도해주세요."
			tmpl, _ := template.ParseFiles("templates/downloadPage.html")
			w.WriteHeader(http.StatusTooManyRequests)
			tmpl.Execute(w, pageData)
			return
		}
	}

	if r.Method == http.MethodGet {
		tmpl, err := template.ParseFiles("templates/downloadPage.html")
		if err != nil {
			http.Error(w, "Template error", http.StatusInternalServerError)
			return
		}
		tmpl.Execute(w, pageData)
		return
	}

	if r.Method == http.MethodPost {
		if pageData.RequirePassword {
			r.ParseForm()
			err = bcrypt.CompareHashAndPassword([]byte(*passwordHash), []byte(r.FormValue("password")))
			if err != nil {
				failLock.Lock()
				failedAttempts[clientIP]++
				failLock.Unlock()

				log.Printf("[SECURITY-AUTH-FAIL] Password mismatch from IP: %s, Share: %s\n", clientIP, shareName)

				pageData.Error = "비밀번호가 일치하지 않습니다."
				tmpl, _ := template.ParseFiles("templates/downloadPage.html")
				w.WriteHeader(http.StatusUnauthorized)
				tmpl.Execute(w, pageData)
				return
			}

			failLock.Lock()
			delete(failedAttempts, clientIP)
			failLock.Unlock()
		}

		_, err = db.Exec(`UPDATE shares SET current_downloads = current_downloads + 1 WHERE share_name = ? AND access_token = ?`, shareName, accessToken)
		if err != nil {
			http.Error(w, "Database error", http.StatusInternalServerError)
			return
		}

		w.Header().Set("Content-Disposition", `attachment; filename="`+originalName+`"`)
		http.ServeFile(w, r, filePath)
	}
}

func main() {
	var port, apiKey, domain string

	flag.StringVar(&dataDir, "datadir", ".", "Directory for config, db, and storage")
	flag.StringVar(&port, "port", "7601", "Server port")
	flag.StringVar(&apiKey, "apikey", "", "API Key for authentication")
	flag.StringVar(&domain, "domain", "http://localhost:7601", "Public Domain")
	flag.Parse()

	appConfig = Config{
		APIKey:         apiKey,
		StorageBaseDir: filepath.Join(dataDir, "sharedrive"),
		Port:           port,
		PublicDomain:   domain,
	}

	os.MkdirAll(dataDir, 0755)
	os.MkdirAll(appConfig.StorageBaseDir, 0755)

	logFilePath := filepath.Join(dataDir, "share-server.log")
	logFile, err := os.OpenFile(logFilePath, os.O_CREATE|os.O_WRONLY|os.O_APPEND, 0666)
	if err == nil {
		log.SetOutput(logFile)
	} else {
		log.Println("경고: 로그 파일을 생성할 수 없어 표준 출력으로 로그를 남깁니다.", err)
	}

	initDB()
	initStorage()
	defer db.Close()

	startGarbageCollector()

	mux := http.NewServeMux()

	mux.Handle("/static/", http.StripPrefix("/static/", http.FileServer(http.Dir("./static"))))
	mux.HandleFunc("/favicon.ico", func(w http.ResponseWriter, r *http.Request) {
		http.ServeFile(w, r, "./static/favicon.ico")
	})

	mux.HandleFunc("GET /api/info", authMiddleware(getInfoHandler))
	mux.HandleFunc("POST /api/shares/init", authMiddleware(initShareHandler))
	mux.HandleFunc("PUT /api/shares/{uuid}/complete", authMiddleware(completeShareHandler))
	mux.HandleFunc("GET /api/shares", authMiddleware(listSharesHandler))
	mux.HandleFunc("PUT /api/shares/{uuid}", authMiddleware(updateShareHandler))
	mux.HandleFunc("PUT /api/shares/{uuid}/status", authMiddleware(updateStatusHandler))
	mux.HandleFunc("DELETE /api/shares/{uuid}", authMiddleware(deleteShareHandler))
	mux.HandleFunc("/", downloadHandler)

	log.Printf("공유 서버가 %s 포트에서 시작되었습니다. (데이터 경로: %s)\n", appConfig.Port, dataDir)
	if err := http.ListenAndServe(":"+appConfig.Port, mux); err != nil {
		log.Fatal(err)
	}
}
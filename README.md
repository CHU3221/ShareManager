# ShareManager

로컬 네트워크(SMB)와 외부 원격 서버를 오가며 쉽고 안전하게 대용량 파일을 공유할 수 있게 해주는 클라이언트-서버 기반 파일 공유 솔루션입니다.

![Dashboard Preview](https://raw.githubusercontent.com/CHU3221/ShareManager/main/docs/ShareManager-preview1.png)
![Dashboard Preview](https://raw.githubusercontent.com/CHU3221/ShareManager/main/docs/ShareManager-preview2.png)
![Dashboard Preview](https://raw.githubusercontent.com/CHU3221/ShareManager/main/docs/ShareManager-preview3.png)

---

## 소개

이 프로젝트는 사용자가 외부 클라우드 서비스에 의존하지 않고, 개인망과 웹 서버를 활용하여 파일 공유 링크를 손쉽게 생성하고 관리할 수 있도록 설계되었습니다.

외부 클라우드 의존도를 최소화하고 개인 환경에 맞춘 최적화가 목표입니다.

주요 목표:

* Local-first 보안 및 프라이버시 유지
* Windows Native 환경의 빠르고 유려한 데스크톱 경험 제공
* 로컬 SMB와 원격 Go 서버 간의 매끄러운 통신
* 직관적인 설치 및 커스터마이징

---

## 목차

1. [주요 특징](#주요-특징)
2. [사용 기술](#사용-기술)
3. [실행 방법](#실행-방법)
4. [사용 방법](#사용-방법)
5. [개발자 가이드](#개발자-가이드)
6. [Credits & Open Source Licenses](#credits--open-source-licenses)

---

## 주요 특징

* **Windows Native UI:** WinUI 3 기반의 빠르고 직관적인 데스크톱 클라이언트 환경 지원
* **외부 클라우드 의존 최소화:** 모든 데이터를 로컬에 저장하며 경량 외부 웹 서버(Go)와 통신


* **보안 공유 설정:** 다운로드 횟수 제한, 만료일 설정, 비밀번호(API Key) 암호화 기능 제공
* **실시간 모니터링:** 활성화된 공유 링크의 상태와 다운로드 트래픽 확인

* **스탠드얼론 모드(개발 중)** 외부 서버 없이 클라이언트에서 실행되는 웹/API서버(go) 기능

---

## 사용 기술

### Backend (`/Server`)

* Go (Golang)
* REST API 기반 비동기 백엔드



### Frontend (`/Client`)

* C# / .NET 8
* WinUI 3 (Windows App SDK)
* CommunityToolkit.Mvvm

### Infra (배포 환경)

* `/Server`
 * Linux 환경
 * Podman / Docker
 * 컨테이너 기반 실행




* `/Client`
 * Windows 환경
 * Standalone 실행 파일 및 MSIX 배포 지원





---

## 실행 방법

### 1. 컨테이너 환경 (`/Server`)

Podman 또는 Docker가 설치된 Linux 서버 환경에서 실행합니다. 적절한 위치로 파일을 복사하여 컨테이너를 시작합니다.

```bash
cd <Containerfile이-있는-경로>
podman stop <컨테이너-이름>
podman rm <컨테이너-이름>
podman build -t localhost/<컨테이너-이름>:latest .

podman run -d \
  --name <컨테이너-이름> \
  --restart always \
  -p <포트넘버>:<포트넘버> \
  -v <SMB-공유-경로(링크파일생성)>:/sharedrive:z \
  -v <Containerfile이-있는-경로>/data:/app/data:z \
  -v /var/log/share-server.log:/var/log/share-server.log:z \
  localhost/<컨테이너-이름>:latest

podman ps -a

```

**설정 주의사항:**
실행 전 `Server/app/data/` 경로에 있는 `config.sample.json`을 `config.json`으로 복사하여 포트 및 API 키를 설정해야 합니다.

---

### 2. 데스크톱 환경 (`/Client`)

Windows 환경에서 직접 빌드하거나 스토어 앱으로 설치하여 사용합니다.

**A. 소스 코드로 직접 실행**

1. Visual Studio 2026에서 `Client/ShareManager.slnx` 솔루션 파일을 엽니다.
2. 패키지를 복원하고 솔루션을 빌드 및 실행합니다.
3. 앱 최초 실행 후 설정 창에서 서버 IP와 API 키를 입력합니다.

**B. 독립형 패키지(MSIX) 배포**
매번 Visual Studio를 열기 번거롭다면, 단일 설치 파일로 만들어 사용할 수 있습니다.

1. 프로젝트를 패키징하여 `.msixupload` 또는 `.msix` 파일을 생성합니다.
2. Windows 환경에서 생성된 파일을 더블클릭하여 앱을 즉시 설치 및 실행할 수 있습니다.

**C. Microsoft Store에서 설치**
1. [다운로드](https://apps.microsoft.com/detail/9N1V9CF9NJPB?hl=ko-kr&gl=KR&ocid=pdpshare)

---

## 사용 방법

앱 내의 직관적인 UI를 통해 각 항목을 설정하고 변경할 수 있습니다.

**환경 설정**

* 톱니바퀴 아이콘을 클릭하여 서버 IP, 포트, API 토큰(비밀번호), Windows 측 SMB 네트워크 경로를 연동합니다.

**새 공유 생성**

* 공유할 파일을 지정하고 만료 날짜, 최대 다운로드 횟수를 설정하여 안전한 공유 링크를 생성합니다.

**목록 관리**

* 현재 공유 중인 항목의 활성화 상태를 확인하고, 필요 시 수동으로 만료 처리할 수 있습니다.

---

## 개발자 가이드

> 이 프로젝트는 홈 네트워크 내부 및 개인용 서버 연동을 전제로 설계되었습니다.
> 
> 

**보안 파일 분리 구조**
보안을 위해 소스 코드 내 하드코딩된 민감 정보는 모두 외부 JSON 파일로 분리되어 있습니다.

* Client: `appsettings.json` (IConfiguration 사용)
* Server: `config.json`

GitHub 커밋 시 실제 설정 파일은 `.gitignore`에 의해 제외되며, 샘플 템플릿 파일만 업로드되도록 구성되어 있습니다.

---

## Credits & Open Source Licenses

이 프로젝트는 다양한 오픈소스 프로젝트의 도움을 받아 제작되었습니다.

### Client Framework

* Windows App SDK (MIT License)
* CommunityToolkit.Mvvm (MIT License)

### Server Framework

* Go Standard Library (BSD License)

### License

이 프로젝트는 **MIT License**를 따릅니다.

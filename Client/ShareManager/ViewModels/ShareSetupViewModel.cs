using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;
using ShareManager.App.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using QRCoder;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;

namespace ShareManager.App.ViewModels
{
    public partial class ShareSetupViewModel : ObservableObject
    {
        private readonly ShareApiService _apiService;
        private readonly TransferService _transferService;
        private readonly SettingsService _settingsService;
        private CancellationTokenSource? _debounceTokenSource;

        public ShareSetupViewModel(ShareApiService apiService, TransferService transferService, SettingsService settingsService)
        {
            _apiService = apiService;
            _transferService = transferService;
            _settingsService = settingsService;

            var nextWeek = DateTime.Now.AddDays(7);
            ExpireYear = nextWeek.Year.ToString();
            ExpireMonth = nextWeek.Month.ToString();
            ExpireDay = nextWeek.Day.ToString();

            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsPasswordEnabled)) OnPropertyChanged(nameof(PasswordVisibility));
                if (e.PropertyName == nameof(IsMaxDownloadsEnabled)) OnPropertyChanged(nameof(MaxDownloadsVisibility));
                if (e.PropertyName == nameof(IsExpireDateEnabled)) OnPropertyChanged(nameof(ExpireDateVisibility));
                if (e.PropertyName == nameof(IsMemoEnabled)) OnPropertyChanged(nameof(MemoVisibility));
                if (e.PropertyName == nameof(IsCompleted))
                {
                    OnPropertyChanged(nameof(SetupVisibility));
                    OnPropertyChanged(nameof(CompletionVisibility));
                }
                if (e.PropertyName == nameof(IsNameValid) || e.PropertyName == nameof(IsWorking) || e.PropertyName == nameof(FilesToShare) || e.PropertyName == nameof(IsExpireDateValid))
                    OnPropertyChanged(nameof(IsShareButtonEnabled));
            };

            FilesToShare.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsShareButtonEnabled));
        }

        [ObservableProperty] public partial ObservableCollection<string> FilesToShare { get; set; } = new();
        [ObservableProperty] public partial string ShareName { get; set; } = string.Empty;
        [ObservableProperty] public partial string NameCheckStatus { get; set; } = string.Empty;
        [ObservableProperty] public partial bool IsNameValid { get; set; } = false;

        [ObservableProperty] public partial bool IsPasswordEnabled { get; set; }
        public Visibility PasswordVisibility => IsPasswordEnabled ? Visibility.Visible : Visibility.Collapsed;
        [ObservableProperty] public partial string Password { get; set; } = string.Empty;

        [ObservableProperty] public partial bool IsMaxDownloadsEnabled { get; set; }
        public Visibility MaxDownloadsVisibility => IsMaxDownloadsEnabled ? Visibility.Visible : Visibility.Collapsed;
        [ObservableProperty] public partial string MaxDownloads { get; set; } = "10";

        [ObservableProperty] public partial bool IsExpireDateEnabled { get; set; }
        public Visibility ExpireDateVisibility => IsExpireDateEnabled ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty] public partial string ExpireYear { get; set; }
        [ObservableProperty] public partial string ExpireMonth { get; set; }
        [ObservableProperty] public partial string ExpireDay { get; set; }

        [ObservableProperty] public partial string ExpireWarningMessage { get; set; } = string.Empty;
        public Visibility ExpireWarningVisibility => string.IsNullOrEmpty(ExpireWarningMessage) ? Visibility.Collapsed : Visibility.Visible;

        [ObservableProperty] public partial bool IsExpireDateValid { get; set; } = true;

        [ObservableProperty] public partial bool IsMemoEnabled { get; set; }
        public Visibility MemoVisibility => IsMemoEnabled ? Visibility.Visible : Visibility.Collapsed;
        [ObservableProperty] public partial string Memo { get; set; } = string.Empty;

        [ObservableProperty] public partial bool IsWorking { get; set; }
        public Visibility ProgressVisibility => IsWorking ? Visibility.Visible : Visibility.Collapsed;
        [ObservableProperty] public partial string ProgressMessage { get; set; } = string.Empty;

        public bool IsShareButtonEnabled => IsNameValid && !IsWorking && FilesToShare.Count > 0 && IsExpireDateValid;

        [ObservableProperty] public partial bool IsCompleted { get; set; } = false;
        public Visibility SetupVisibility => IsCompleted ? Visibility.Collapsed : Visibility.Visible;
        public Visibility CompletionVisibility => IsCompleted ? Visibility.Visible : Visibility.Collapsed;

        [ObservableProperty] public partial string FinalUrl { get; set; } = string.Empty;
        [ObservableProperty] public partial BitmapImage? QrCodeImage { get; set; }

        partial void OnShareNameChanged(string value)
        {
            _debounceTokenSource?.Cancel();
            _debounceTokenSource = new CancellationTokenSource();

            if (string.IsNullOrWhiteSpace(value))
            {
                NameCheckStatus = "공유 이름을 입력해주세요.";
                IsNameValid = false;
                return;
            }

            NameCheckStatus = "중복 확인 중...";
            _ = CheckShareNameAsync(value, _debounceTokenSource.Token);
        }

        private async Task CheckShareNameAsync(string name, CancellationToken token)
        {
            try
            {
                await Task.Delay(500, token);
                if (token.IsCancellationRequested) return;

                var shares = await _apiService.GetSharesAsync();
                bool isDuplicate = shares != null && shares.Any(s => s.share_name == name);

                if (isDuplicate)
                {
                    NameCheckStatus = "이미 사용 중인 이름입니다.";
                    IsNameValid = false;
                }
                else
                {
                    NameCheckStatus = "사용 가능한 이름입니다.";
                    IsNameValid = true;
                }
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                NameCheckStatus = $"확인 실패: {ex.Message}";
                IsNameValid = false;
            }
        }

        partial void OnIsExpireDateEnabledChanged(bool value) => ValidateExpireDate();
        partial void OnExpireYearChanged(string value) => ValidateExpireDate();
        partial void OnExpireMonthChanged(string value) => ValidateExpireDate();
        partial void OnExpireDayChanged(string value) => ValidateExpireDate();

        private void ValidateExpireDate()
        {
            if (!IsExpireDateEnabled)
            {
                ExpireWarningMessage = string.Empty;
                IsExpireDateValid = true;
                return;
            }

            if (!int.TryParse(ExpireYear, out int year) || !int.TryParse(ExpireMonth, out int month) || !int.TryParse(ExpireDay, out int day))
            {
                ExpireWarningMessage = "올바른 숫자를 입력해 주세요.";
                IsExpireDateValid = false;
            }
            else if (year < 2000 || year > 2100)
            {
                ExpireWarningMessage = "연도를 올바르게 입력해 주세요.";
                IsExpireDateValid = false;
            }
            else if (month < 1 || month > 12)
            {
                ExpireWarningMessage = "월은 1~12 사이여야 합니다.";
                IsExpireDateValid = false;
            }
            else if (day < 1 || day > DateTime.DaysInMonth(year, month))
            {
                ExpireWarningMessage = $"{month}월에는 {day}일이 존재하지 않습니다.";
                IsExpireDateValid = false;
            }
            else
            {
                var endOfDay = new DateTime(year, month, day, 23, 59, 59, DateTimeKind.Local);
                if (endOfDay < DateTime.Now)
                {
                    ExpireWarningMessage = "과거 날짜는 설정할 수 없습니다.";
                    IsExpireDateValid = false;
                }
                else
                {
                    ExpireWarningMessage = string.Empty;
                    IsExpireDateValid = true;
                }
            }

            OnPropertyChanged(nameof(ExpireWarningVisibility));
            OnPropertyChanged(nameof(IsExpireDateValid));
        }


        [RelayCommand]
        public void RemoveFile(string filePath)
        {
            if (FilesToShare.Contains(filePath)) FilesToShare.Remove(filePath);
        }

        [RelayCommand]
        public void Cancel()
        {
            App.Current.AppMainWindow?.NavigateToManagement();
        }

        [RelayCommand]
        public async Task StartShareAsync()
        {
            if (!IsShareButtonEnabled) return;

            IsWorking = true;
            OnPropertyChanged(nameof(ProgressVisibility));

            try
            {
                string originalName = FilesToShare.Count == 1 && File.Exists(FilesToShare[0])
                    ? Path.GetFileName(FilesToShare[0])
                    : $"{ShareName}.zip";

                string? formattedExpireDate = null;
                if (IsExpireDateEnabled && int.TryParse(ExpireYear, out int y) && int.TryParse(ExpireMonth, out int m) && int.TryParse(ExpireDay, out int d))
                {
                    var endOfDay = new DateTime(y, m, d, 23, 59, 59, DateTimeKind.Local);
                    formattedExpireDate = endOfDay.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
                }

                ProgressMessage = "서버에 공유 등록 중...";
                var initRequest = new ShareInitRequest
                {
                    share_name = ShareName,
                    original_name = originalName,
                    expire_at = formattedExpireDate,
                    password_hash = IsPasswordEnabled ? Password : null,
                    memo = IsMemoEnabled ? Memo : "",
                    max_downloads = IsMaxDownloadsEnabled && int.TryParse(MaxDownloads, out int md) ? md : 0
                };

                var initResponse = await _apiService.InitShareAsync(initRequest);

                ProgressMessage = FilesToShare.Count > 1 ? "파일 압축 및 전송 중..." : "파일 전송 중...";
                await _transferService.ProcessTransferAsync(FilesToShare.ToArray(), _settingsService.SmbBasePath, initResponse.uuid, originalName);

                ProgressMessage = "최종 확인 중...";
                await _apiService.CompleteShareAsync(initResponse.uuid);

                var serverInfo = await _apiService.GetServerInfoAsync();
                string domain = string.IsNullOrWhiteSpace(serverInfo.public_domain)
                                ? _settingsService.GetBaseUrl()
                                : serverInfo.public_domain;

                domain = domain.TrimEnd('/');
                FinalUrl = $"{domain}/{ShareName}-{initResponse.access_token}";

                await GenerateQRCodeAsync(FinalUrl);

                IsWorking = false;
                IsCompleted = true;
                OnPropertyChanged(nameof(ProgressVisibility));
            }
            catch (Exception ex)
            {
                ProgressMessage = $"오류 발생:\n{ex.Message}";
                await Task.Delay(3000);
                IsWorking = false;
                OnPropertyChanged(nameof(ProgressVisibility));
            }
        }

        private async Task GenerateQRCodeAsync(string text)
        {
            using var generator = new QRCodeGenerator();
            using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qr = new PngByteQRCode(data);
            byte[] qrBytes = qr.GetGraphic(10);

            var stream = new InMemoryRandomAccessStream();
            await stream.WriteAsync(qrBytes.AsBuffer());
            stream.Seek(0);

            var image = new BitmapImage();
            await image.SetSourceAsync(stream);
            QrCodeImage = image;
        }

        [RelayCommand]
        public void CopyToClipboard()
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(FinalUrl);
            Clipboard.SetContent(dataPackage);
        }

        [RelayCommand]
        public void FinishAndReturn()
        {
            App.Current.AppMainWindow?.NavigateToManagement();
        }
    }
}
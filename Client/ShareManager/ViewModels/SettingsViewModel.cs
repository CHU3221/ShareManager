using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareManager.App.Services;
using System;
using System.IO;

namespace ShareManager.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        private bool _originalIsStandalone;
        private string _originalPort = string.Empty;
        private string _originalServerIp = string.Empty;

        public bool IsExternalServerMode => !IsStandaloneMode;

        [ObservableProperty]
        public partial bool IsStandaloneMode { get; set; }

        [ObservableProperty]
        public partial string ServerIp { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Port { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ApiKey { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SmbBasePath { get; set; } = string.Empty;

        // Win11 스타일 에러/경고 표시용 InfoBar 바인딩
        [ObservableProperty]
        public partial bool ShowError { get; set; }

        [ObservableProperty]
        public partial string ErrorMessage { get; set; } = string.Empty;

        [ObservableProperty]
        public partial bool ShowSuccess { get; set; }

        [ObservableProperty]
        public partial string SuccessMessage { get; set; } = string.Empty;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            IsStandaloneMode = _settingsService.IsStandaloneMode;
            ServerIp = _settingsService.ServerIp;
            Port = _settingsService.Port;
            ApiKey = _settingsService.ApiKey;
            SmbBasePath = _settingsService.SmbBasePath;

            _originalIsStandalone = IsStandaloneMode;
            _originalServerIp = ServerIp;
            _originalPort = Port;

            UpdateModeFields();
        }

        partial void OnIsStandaloneModeChanged(bool value)
        {
            UpdateModeFields();
            ShowError = false;

            OnPropertyChanged(nameof(IsExternalServerMode));
        }

        private void UpdateModeFields()
        {
            if (IsStandaloneMode)
            {
                // localhost 대신 명시적인 내부 IP 사용
                ServerIp = "127.0.0.1";
                Port = "7601";
                SmbBasePath = Path.Combine(_settingsService.LocalDataPath, "sharedrive");
            }
        }

        public bool ValidateInput()
        {
            ShowError = false;
            ShowSuccess = false;

            if (!IsStandaloneMode)
            {
                if (string.IsNullOrWhiteSpace(ServerIp) || ServerIp.ToLower() == "localhost" || ServerIp == "127.0.0.1")
                {
                    ErrorMessage = "외부 서버 모드에서는 'localhost'나 루프백 주소를 사용할 수 없습니다.";
                    ShowError = true;
                    return false;
                }

                if (string.IsNullOrWhiteSpace(SmbBasePath) || !SmbBasePath.StartsWith(@"\\"))
                {
                    ErrorMessage = "올바른 Windows SMB 네트워크 경로(\\\\)를 입력해주세요.";
                    ShowError = true;
                    return false;
                }
            }

            return true;
        }

        public bool RequiresRestart()
        {
            return _originalIsStandalone != IsStandaloneMode ||
                   _originalServerIp != ServerIp ||
                   _originalPort != Port;
        }

        public void ApplySettings()
        {
            _settingsService.IsStandaloneMode = IsStandaloneMode;
            _settingsService.ServerIp = ServerIp;
            _settingsService.Port = Port;
            _settingsService.ApiKey = ApiKey;
            _settingsService.SmbBasePath = SmbBasePath;
        }

        [RelayCommand]
        public void CleanUpLocalData()
        {
            try
            {
                string targetDir = Path.Combine(_settingsService.LocalDataPath, "sharedrive");
                if (Directory.Exists(targetDir))
                {
                    Directory.Delete(targetDir, true);
                    Directory.CreateDirectory(targetDir);

                    SuccessMessage = "로컬 공유 데이터가 깨끗하게 정리되었습니다.";
                    ShowSuccess = true;
                    ShowError = false;
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"데이터 정리 중 오류가 발생했습니다: {ex.Message}";
                ShowError = true;
                ShowSuccess = false;
            }
        }

        [RelayCommand]
        public void Cancel()
        {
            App.Current.AppMainWindow?.NavigateToManagement();
        }
    }
}
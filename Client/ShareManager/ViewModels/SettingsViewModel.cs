using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareManager.App.Services;

namespace ShareManager.App.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;

        [ObservableProperty]
        public partial string ServerIp { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string Port { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string ApiKey { get; set; } = string.Empty;

        [ObservableProperty]
        public partial string SmbBasePath { get; set; } = string.Empty;

        public SettingsViewModel(SettingsService settingsService)
        {
            _settingsService = settingsService;
            LoadSettings();
        }

        private void LoadSettings()
        {
            ServerIp = _settingsService.ServerIp;
            Port = _settingsService.Port;
            ApiKey = _settingsService.ApiKey;
            SmbBasePath = _settingsService.SmbBasePath;
        }

        [RelayCommand]
        public void Save()
        {
            _settingsService.ServerIp = ServerIp;
            _settingsService.Port = Port;
            _settingsService.ApiKey = ApiKey;
            _settingsService.SmbBasePath = SmbBasePath;

            App.Current.AppMainWindow?.NavigateToManagement();
        }

        [RelayCommand]
        public void Cancel()
        {
            App.Current.AppMainWindow?.NavigateToManagement();
        }
    }
}
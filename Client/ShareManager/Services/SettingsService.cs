using Microsoft.Extensions.Configuration;
using Windows.Storage;

namespace ShareManager.App.Services
{
    public class SettingsService
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.Current.LocalSettings;
        private readonly IConfiguration _configuration;

        public SettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ServerIp
        {
            get => _localSettings.Values["ServerIp"] as string
                ?? _configuration.GetValue<string>("Server:ServerIp")
                ?? "192.168.1.100";
            set => _localSettings.Values["ServerIp"] = value;
        }

        public string Port
        {
            get => _localSettings.Values["Port"] as string
                ?? _configuration.GetValue<string>("Server:Port")
                ?? "7601";
            set => _localSettings.Values["Port"] = value;
        }

        public string ApiKey
        {
            get => _localSettings.Values["ApiKey"] as string
                ?? _configuration.GetValue<string>("Server:ApiKey")
                ?? string.Empty;
            set => _localSettings.Values["ApiKey"] = value;
        }

        public string SmbBasePath
        {
            get => _localSettings.Values["SmbBasePath"] as string
                ?? _configuration.GetValue<string>("Server:SmbBasePath")
                ?? @"\\192.168.1.100\SharedLinks";
            set => _localSettings.Values["SmbBasePath"] = value;
        }

        public string GetBaseUrl() => $"http://{ServerIp}:{Port}";
    }
}

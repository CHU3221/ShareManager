using Microsoft.Extensions.Configuration;
using System;
using System.IO;
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

            if (!Directory.Exists(LocalDataPath))
            {
                Directory.CreateDirectory(LocalDataPath);
            }
        }

        public bool IsStandaloneMode
        {
            get => _localSettings.Values["IsStandaloneMode"] as bool? ?? true;
            set => _localSettings.Values["IsStandaloneMode"] = value;
        }

        public string LocalDataPath => ApplicationData.Current.LocalFolder.Path;

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
            get
            {
                var existing = _localSettings.Values["ApiKey"] as string;
                if (string.IsNullOrEmpty(existing))
                {
                    string newKey = Guid.NewGuid().ToString("N");
                    _localSettings.Values["ApiKey"] = newKey;
                    return newKey;
                }
                return existing;
            }
            set => _localSettings.Values["ApiKey"] = value;
        }

        public string SmbBasePath
        {
            get
            {
                if (IsStandaloneMode)
                    return Path.Combine(LocalDataPath, "sharedrive");

                return _localSettings.Values["SmbBasePath"] as string
                    ?? _configuration.GetValue<string>("Server:SmbBasePath")
                    ?? @"\\192.168.1.100\SharedLinks";
            }
            set => _localSettings.Values["SmbBasePath"] = value;
        }

        public string GetBaseUrl()
        {
            if (IsStandaloneMode)
                return $"http://127.0.0.1:{Port}";

            return $"http://{ServerIp}:{Port}";
        }
    }
}
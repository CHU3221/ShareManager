using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using ShareManager.App.ViewModels;
using ShareManager.App.Services;
using System;
using System.Diagnostics;
using System.IO;

namespace ShareManager.App
{
    public partial class App : Application
    {
        public MainWindow? AppMainWindow { get; private set; }
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }
        private Process? _localGoServerProcess;

        public App()
        {
            this.InitializeComponent();
            Services = ConfigureServices();
        }

        private IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();

            services.AddSingleton<IConfiguration>(configuration);

            services.AddSingleton<SettingsService>();
            services.AddSingleton<TransferService>();
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<SettingsService>();
                return new ShareApiService(settings.GetBaseUrl(), settings.ApiKey);
            });

            services.AddTransient<ManagementViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<ShareSetupViewModel>();


            return services.BuildServiceProvider();
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            AppMainWindow = new MainWindow();

            AppMainWindow.Closed += MainWindow_Closed;

            var settings = Services.GetRequiredService<SettingsService>();
            if (settings.IsStandaloneMode)
            {
                StartLocalServer(settings);
            }

            AppMainWindow.NavigateToManagement();
            AppMainWindow.Activate();
        }

        private void StartLocalServer(SettingsService settings)
        {
            try
            {
                string serverExePath = Path.Combine(AppContext.BaseDirectory, "Assets", "server.exe");

                if (File.Exists(serverExePath))
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = serverExePath,
                        WorkingDirectory = Path.GetDirectoryName(serverExePath),
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    psi.ArgumentList.Add("-datadir");
                    psi.ArgumentList.Add(settings.LocalDataPath);

                    psi.ArgumentList.Add("-port");
                    psi.ArgumentList.Add(settings.Port);

                    psi.ArgumentList.Add("-apikey");
                    psi.ArgumentList.Add(settings.ApiKey);

                    _localGoServerProcess = Process.Start(psi);
                    Debug.WriteLine("[로컬 서버 실행 성공]");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[로컬 서버 실행 실패] {ex.Message}");
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_localGoServerProcess != null && !_localGoServerProcess.HasExited)
            {
                try
                {
                    _localGoServerProcess.Kill();
                    _localGoServerProcess.Dispose();
                }
                catch {  }
            }
        }


    }
}
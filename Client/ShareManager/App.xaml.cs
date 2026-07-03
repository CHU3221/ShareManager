using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.UI.Xaml;
using ShareManager.App.ViewModels;
using ShareManager.App.Services;
using System;
using System.Linq;

namespace ShareManager.App
{
    public partial class App : Application
    {
        public MainWindow? AppMainWindow { get; private set; }
        public new static App Current => (App)Application.Current;
        public IServiceProvider Services { get; }

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
            AppMainWindow.NavigateToManagement();
            AppMainWindow.Activate();
        }
    }
}
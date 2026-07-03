using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShareManager.App.ViewModels;

namespace ShareManager.App.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsViewModel? ViewModel { get; private set; }

        public SettingsView()
        {
            this.InitializeComponent();

            var app = Microsoft.UI.Xaml.Application.Current as App;
            if (app?.Services != null)
            {
                ViewModel = app.Services.GetRequiredService<SettingsViewModel>();
                this.DataContext = ViewModel;
            }
        }
    }
}
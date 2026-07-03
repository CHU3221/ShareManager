using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShareManager.App.ViewModels;

namespace ShareManager.App.Views
{
    public sealed partial class ManagementView : Page
    {
        public ManagementViewModel? ViewModel { get; private set; }

        public ManagementView()
        {
            this.InitializeComponent();

            ViewModel = App.Current.Services.GetRequiredService<ManagementViewModel>();
            this.DataContext = ViewModel;

            this.Loaded += (s, e) => { _ = ViewModel.LoadDataAsync(); };
        }

        private void GoToSettings_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            App.Current.AppMainWindow?.NavigateToSettings();
        }

        private void GoToShareSetup_Click(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            App.Current.AppMainWindow?.NavigateToShareSetup();
        }
    }


}
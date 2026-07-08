using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShareManager.App.ViewModels;
using System;

namespace ShareManager.App.Views
{
    public sealed partial class SettingsView : Page
    {
        public SettingsViewModel? ViewModel { get; private set; }

        public SettingsView()
        {
            this.InitializeComponent();

            var app = Application.Current as App;
            if (app?.Services != null)
            {
                ViewModel = app.Services.GetRequiredService<SettingsViewModel>();
                this.DataContext = ViewModel;
            }
        }

        private async void OnSaveClicked(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            if (!ViewModel.ValidateInput()) return;

            if (ViewModel.RequiresRestart())
            {
                ContentDialog dialog = new ContentDialog
                {
                    XamlRoot = this.XamlRoot,
                    Title = "앱 재시작 필요",
                    Content = "서버 모드나 IP, 포트 설정이 변경되었습니다. 완벽한 적용을 위해 앱을 다시 시작해야 합니다.\n지금 재시작하시겠습니까?",
                    PrimaryButtonText = "저장 및 재시작",
                    CloseButtonText = "취소",
                    DefaultButton = ContentDialogButton.Primary
                };

                var result = await dialog.ShowAsync();

                if (result == ContentDialogResult.Primary)
                {
                    ViewModel.ApplySettings();
                    Microsoft.Windows.AppLifecycle.AppInstance.Restart("");
                }
            }
            else
            {
                ViewModel.ApplySettings();
                App.Current.AppMainWindow?.NavigateToManagement();
            }
        }
    }
}
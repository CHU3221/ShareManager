using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ShareManager.App.Views;
using WinRT.Interop;

namespace ShareManager.App
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            SetWindowIcon();
        }

        private void SetWindowIcon()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");

            if (appWindow is not null && File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }

        public void NavigateToManagement()
        {
            if (RootFrame is null)
            {
                _ = ShowErrorAsync("Internal error: UI frame is not initialized.");
                return;
            }

            try
            {
                RootFrame.Navigate(typeof(ManagementView));
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("Unable to navigate to Management view. " + ex.Message);
            }
        }

        public void NavigateToShareSetup()
        {
            if (RootFrame is null)
            {
                _ = ShowErrorAsync("Internal error: UI frame is not initialized.");
                return;
            }

            try
            {
                RootFrame.Navigate(typeof(ShareSetupView));
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("Unable to navigate to Share Setup view. " + ex.Message);
            }
        }

        public void NavigateToSettings()
        {
            if (RootFrame is null)
            {
                _ = ShowErrorAsync("Internal error: UI frame is not initialized.");
                return;
            }

            try
            {
                RootFrame.Navigate(typeof(SettingsView));
            }
            catch (Exception ex)
            {
                _ = ShowErrorAsync("Unable to navigate to Settings view. " + ex.Message);
            }
        }

        private async Task ShowErrorAsync(string message, string title = "Error")
        {
            try
            {
                var dialog = new ContentDialog
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK",
                    XamlRoot = this.Content?.XamlRoot
                };

                if (dialog.XamlRoot is null)
                {
                    return;
                }

                await dialog.ShowAsync();
            }
            catch
            {
            }
        }
    }
}

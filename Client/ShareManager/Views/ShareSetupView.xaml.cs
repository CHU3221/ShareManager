using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Extensions.DependencyInjection;
using ShareManager.App.ViewModels;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using Windows.ApplicationModel.DataTransfer;

namespace ShareManager.App.Views
{
    public sealed partial class ShareSetupView : Page
    {
        public ShareSetupViewModel? ViewModel { get; private set; }

        public ShareSetupView()
        {
            this.InitializeComponent();

            var app = Application.Current as App;
            if (app?.Services != null)
            {
                ViewModel = app.Services.GetRequiredService<ShareSetupViewModel>();
                this.DataContext = ViewModel;
            }
        }

        private async void AddFile_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.AppMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && ViewModel != null)
            {
                foreach (var file in files)
                {
                    if (!ViewModel.FilesToShare.Contains(file.Path))
                        ViewModel.FilesToShare.Add(file.Path);
                }
            }
        }

        private async void AddFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
            picker.FileTypeFilter.Add("*");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.Current.AppMainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null && ViewModel != null)
            {
                if (!ViewModel.FilesToShare.Contains(folder.Path))
                    ViewModel.FilesToShare.Add(folder.Path);
            }
        }

        private void Grid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                DragDropOverlay.Visibility = Visibility.Visible;
            }
        }

        private void Grid_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "공유 목록에 추가";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
        }

        private void Grid_DragLeave(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;
        }

        private async void Grid_Drop(object sender, DragEventArgs e)
        {
            DragDropOverlay.Visibility = Visibility.Collapsed;

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (ViewModel != null)
                {
                    foreach (var item in items)
                    {
                        if (!ViewModel.FilesToShare.Contains(item.Path))
                        {
                            ViewModel.FilesToShare.Add(item.Path);
                        }
                    }
                }
            }
        }

        private void ExitApp_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Exit();
        }
    }
}
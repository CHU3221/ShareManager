using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareManager.App.Services;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace ShareManager.App.ViewModels
{
    public partial class ObservableShareItem : ObservableObject
    {
        private readonly ShareItem _original;
        private readonly Action<ObservableShareItem> _onToggleAction;
        private bool _isInitializing = true;

        public string Uuid => _original.uuid;
        public string ShareName => _original.share_name;
        public string AccessToken => _original.access_token;
        public string OriginalName => _original.original_name;

        public string CreatedAt
        {
            get
            {
                if (DateTime.TryParse(_original.created_at, out var dt))
                    return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                return _original.created_at;
            }
        }

        public int CurrentDownloads => _original.current_downloads;

        public string PasswordPlaceholder => HasPassword ? "암호로 보호됨" : (IsPasswordRemoved ? "해제 저장 대기중..." : "");

        [ObservableProperty]
        public partial bool IsActive { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(PasswordPlaceholder))]
        public partial bool HasPassword { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        [NotifyPropertyChangedFor(nameof(PasswordPlaceholder))]
        public partial bool IsPasswordRemoved { get; set; }

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        public partial string MaxDownloadsText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        public partial string ExpireDateText { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        public partial string NewPassword { get; set; } = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsDirty))]
        public partial string Memo { get; set; } = string.Empty;

        public ObservableShareItem(ShareItem original, Action<ObservableShareItem> onToggleAction)
        {
            _original = original;
            _onToggleAction = onToggleAction;

            IsActive = original.status == "active";
            HasPassword = original.has_password;
            Memo = original.memo ?? string.Empty;

            MaxDownloadsText = original.max_downloads <= 0 ? "무제한" : original.max_downloads.ToString();
            ExpireDateText = string.IsNullOrEmpty(original.expire_at) ? "" :
                (DateTime.TryParse(original.expire_at, out var ed) ? ed.ToLocalTime().ToString("yyyy-MM-dd") : original.expire_at);

            _isInitializing = false;
        }

        [RelayCommand]
        private void RemovePassword()
        {
            IsPasswordRemoved = true;
            HasPassword = false;
            NewPassword = string.Empty;
        }

        partial void OnIsActiveChanged(bool value)
        {
            if (!_isInitializing) _onToggleAction?.Invoke(this);
        }

        public bool IsDirty
        {
            get
            {
                bool memoChanged = Memo != (_original.memo ?? string.Empty);
                bool passwordChanged = !string.IsNullOrEmpty(NewPassword) || IsPasswordRemoved;

                int parsedMax = int.TryParse(MaxDownloadsText, out int m) ? m : 0;
                bool maxChanged = parsedMax != _original.max_downloads;

                string origExpire = string.IsNullOrEmpty(_original.expire_at) ? "" : DateTime.Parse(_original.expire_at).ToLocalTime().ToString("yyyy-MM-dd");
                string currentExpire = ExpireDateText.Trim();
                bool expireChanged = origExpire != currentExpire;

                return maxChanged || memoChanged || passwordChanged || expireChanged;
            }
        }

        public void ResetDirty()
        {
            int savedMax = int.TryParse(MaxDownloadsText, out int m) ? m : 0;
            _original.max_downloads = savedMax;
            MaxDownloadsText = savedMax <= 0 ? "무제한" : savedMax.ToString();

            _original.memo = Memo;

            if (DateTime.TryParse(ExpireDateText, out var dt))
            {
                var endOfDay = new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Local);
                _original.expire_at = endOfDay.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            }
            else
            {
                _original.expire_at = null;
                ExpireDateText = string.Empty;
            }

            if (IsPasswordRemoved)
            {
                _original.has_password = false;
                IsPasswordRemoved = false;
            }
            else if (!string.IsNullOrEmpty(NewPassword))
            {
                _original.has_password = true;
                NewPassword = string.Empty;
            }

            HasPassword = _original.has_password;
            OnPropertyChanged(nameof(PasswordPlaceholder));
            OnPropertyChanged(nameof(IsDirty));
        }
    }

    public partial class ManagementViewModel : ObservableObject
    {
        private readonly ShareApiService _apiService;
        private string _publicDomain = string.Empty;

        [ObservableProperty]
        public partial ObservableCollection<ObservableShareItem> ShareItems { get; set; } = new();

        [ObservableProperty]
        public partial bool IsLoading { get; set; }

        public ManagementViewModel(ShareApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var info = await _apiService.GetServerInfoAsync();
                _publicDomain = info.public_domain.TrimEnd('/');

                var rawItems = await _apiService.GetSharesAsync();
                ShareItems.Clear();
                foreach (var item in rawItems)
                {
                    ShareItems.Add(new ObservableShareItem(item, async (changedItem) => await ToggleStatusAsync(changedItem)));
                }
            }
            catch { }
            finally { IsLoading = false; }
        }

        private async Task ToggleStatusAsync(ObservableShareItem item)
        {
            string newStatus = item.IsActive ? "active" : "inactive";
            await _apiService.UpdateStatusAsync(item.Uuid, newStatus);
        }

        [RelayCommand]
        private async Task SaveChangesAsync(ObservableShareItem item)
        {
            if (!item.IsDirty) return;

            var request = new UpdateShareRequest
            {
                max_downloads = int.TryParse(item.MaxDownloadsText, out int md) ? md : 0,
                memo = item.Memo,
                expire_at = DateTime.TryParse(item.ExpireDateText, out var dt)
                ? new DateTime(dt.Year, dt.Month, dt.Day, 23, 59, 59, DateTimeKind.Local).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
                : null,
                update_password = !string.IsNullOrEmpty(item.NewPassword) || item.IsPasswordRemoved,
                password = item.IsPasswordRemoved ? "" : item.NewPassword
            };

            await _apiService.UpdateShareAsync(item.Uuid, request);
            item.ResetDirty();
        }

        [RelayCommand]
        private void CopyLink(ObservableShareItem item)
        {
            var package = new DataPackage();
            package.SetText($"{_publicDomain}/{item.ShareName}-{item.AccessToken}");
            Clipboard.SetContent(package);
        }

        [RelayCommand]
        private async Task DeleteShareAsync(ObservableShareItem item)
        {
            await _apiService.DeleteShareAsync(item.Uuid);
            ShareItems.Remove(item);
        }
    }
}
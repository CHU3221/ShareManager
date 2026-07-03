namespace ShareManager.App.Services
{
    public class ShareItem
    {
        public string uuid { get; set; } = string.Empty;
        public string share_name { get; set; } = string.Empty;
        public string access_token { get; set; } = string.Empty;
        public string original_name { get; set; } = string.Empty;
        public string status { get; set; } = string.Empty;
        public string? expire_at { get; set; }
        public string created_at { get; set; } = string.Empty;
        public string memo { get; set; } = string.Empty;
        public int max_downloads { get; set; }
        public int current_downloads { get; set; }
        public bool has_password { get; set; }
    }
}

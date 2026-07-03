using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace ShareManager.App.Services
{
    public class ShareApiService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;

        public ShareApiService(string baseUrl, string apiKey)
        {
            _apiKey = apiKey;
            _httpClient = new HttpClient { BaseAddress = new System.Uri(baseUrl) };
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        public async Task<ServerInfoResponse> GetServerInfoAsync()
        {
            var response = await _httpClient.GetAsync("/api/info");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ServerInfoResponse>() ?? new ServerInfoResponse();
        }

        public async Task<ShareInitResponse> InitShareAsync(ShareInitRequest request)
        {
            var response = await _httpClient.PostAsJsonAsync("/api/shares/init", request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ShareInitResponse>() ?? new ShareInitResponse();
        }

        public async Task CompleteShareAsync(string uuid)
        {
            var response = await _httpClient.PutAsync($"/api/shares/{uuid}/complete", null);
            response.EnsureSuccessStatusCode();
        }

        public async Task<ShareItem[]> GetSharesAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ShareItem[]>("/api/shares") ?? System.Array.Empty<ShareItem>();
            }
            catch
            {
                throw;
            }
        }

        public async Task UpdateShareAsync(string uuid, UpdateShareRequest request)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shares/{uuid}", request);
            response.EnsureSuccessStatusCode();
        }

        public async Task DeleteShareAsync(string uuid)
        {
            var response = await _httpClient.DeleteAsync($"/api/shares/{uuid}");
            response.EnsureSuccessStatusCode();
        }

        public async Task UpdateStatusAsync(string uuid, string status)
        {
            var response = await _httpClient.PutAsJsonAsync($"/api/shares/{uuid}/status", new { status = status });
            response.EnsureSuccessStatusCode();
        }
    }

    public class ServerInfoResponse
    {
        public string public_domain { get; set; } = string.Empty;
    }

    public class ShareInitRequest
    {
        public string? share_name { get; set; }
        public string? original_name { get; set; }
        public string? expire_at { get; set; }
        public string? password_hash { get; set; }
        public string? memo { get; set; }
        public int max_downloads { get; set; }
    }

    public class ShareInitResponse
    {
        public string uuid { get; set; } = string.Empty;
        public string access_token { get; set; } = string.Empty;
    }

    public class UpdateShareRequest
    {
        public string? expire_at { get; set; }
        public bool update_password { get; set; }
        public string? password { get; set; }
        public string memo { get; set; } = string.Empty;
        public int max_downloads { get; set; }
    }

}

using System;
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
            _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

            _httpClient.Timeout = TimeSpan.FromSeconds(7);

            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
        }

        private async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (TaskCanceledException)
            {
                throw new Exception("서버 응답 시간이 초과되었습니다. (Timeout)");
            }
            catch (HttpRequestException ex)
            {
                throw new Exception($"통신 실패 (상세 이유: {ex.Message})");
            }
        }

        public async Task<ServerInfoResponse> GetServerInfoAsync()
        {
            return await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.GetAsync("/api/info");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ServerInfoResponse>() ?? new ServerInfoResponse();
            });
        }

        public async Task<ShareInitResponse> InitShareAsync(ShareInitRequest request)
        {
            return await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.PostAsJsonAsync("/api/shares/init", request);
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadFromJsonAsync<ShareInitResponse>() ?? new ShareInitResponse();
            });
        }

        public async Task CompleteShareAsync(string uuid)
        {
            await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.PutAsync($"/api/shares/{uuid}/complete", null);
                response.EnsureSuccessStatusCode();
                return true;
            });
        }

        public async Task<ShareItem[]> GetSharesAsync()
        {
            return await ExecuteWithTimeoutAsync(async () =>
            {
                return await _httpClient.GetFromJsonAsync<ShareItem[]>("/api/shares") ?? Array.Empty<ShareItem>();
            });
        }

        public async Task UpdateShareAsync(string uuid, UpdateShareRequest request)
        {
            await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/shares/{uuid}", request);
                response.EnsureSuccessStatusCode();
                return true;
            });
        }

        public async Task DeleteShareAsync(string uuid)
        {
            await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.DeleteAsync($"/api/shares/{uuid}");
                response.EnsureSuccessStatusCode();
                return true;
            });
        }

        public async Task UpdateStatusAsync(string uuid, string status)
        {
            await ExecuteWithTimeoutAsync(async () =>
            {
                var response = await _httpClient.PutAsJsonAsync($"/api/shares/{uuid}/status", new { status = status });
                response.EnsureSuccessStatusCode();
                return true;
            });
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

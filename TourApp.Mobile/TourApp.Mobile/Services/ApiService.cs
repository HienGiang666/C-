using System.Diagnostics;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        // IP WiFi hiện tại của máy dev — cập nhật nếu đổi mạng
        private const string DefaultUrl = "http://192.168.1.5:5254";

        public static string BaseUrl
        {
            get => Preferences.Default.Get("api_base_url", DefaultUrl);
            set => Preferences.Default.Set("api_base_url", value);
        }

        public static void UpdateBaseUrl(string newUrl) =>
            BaseUrl = newUrl.TrimEnd('/');

        /// <summary>
        /// Gọi 1 lần sau khi app khởi động xong để reset IP cũ nếu cần.
        /// KHÔNG gọi trong static constructor — Preferences chưa sẵn sàng lúc đó.
        /// </summary>
        public static void ResetCachedUrlIfNeeded()
        {
            try
            {
                var cached = Preferences.Default.Get("api_base_url", "");
                if (!string.IsNullOrEmpty(cached) && cached != DefaultUrl)
                {
                    Preferences.Default.Remove("api_base_url");
                    Debug.WriteLine($"[ApiService] Reset cached URL: {cached} → {DefaultUrl}");
                }
            }
            catch { /* Preferences chưa sẵn sàng — bỏ qua */ }
        }

        [DebuggerNonUserCode]
        public Task<bool> TestConnectionAsync() =>
            TryFetch("/api/poi", _ => true, () => false);

        [DebuggerNonUserCode]
        public Task<List<POI>> GetAllPOIsAsync() =>
            TryFetch<List<POI>>(
                "/api/poi",
                body =>
                {
                    var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    return JsonSerializer.Deserialize<List<POI>>(body, opts) ?? new();
                },
                () => new List<POI>()
            );

        public Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi") =>
            Task.FromResult<Audio?>(null);

        public Task LogNarrationAsync(int poiId, int? audioId, string triggerType) =>
            Task.CompletedTask;

        [DebuggerNonUserCode]
        private static Task<T> TryFetch<T>(
            string path,
            Func<string, T> parse,
            Func<T> fallback)
        {
            return Task.Run(async () =>
            {
                HttpClient? client = null;
                try
                {
                    client = CreateClient();
                    var getTask = client.GetAsync(path);
                    var winner = await Task.WhenAny(getTask, Task.Delay(5000)).ConfigureAwait(false);

                    if (winner != getTask || !getTask.IsCompletedSuccessfully)
                    {
                        Debug.WriteLine($"[ApiService] {path}: timeout/error");
                        return fallback();
                    }

                    var response = getTask.Result;
                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[ApiService] {path}: HTTP {(int)response.StatusCode}");
                        return fallback();
                    }

                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return parse(body);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ApiService] {path}: {ex.Message}");
                    return fallback();
                }
                finally
                {
                    client?.Dispose();
                }
            });
        }

        [DebuggerNonUserCode]
        private static HttpClient CreateClient()
        {
            HttpMessageHandler handler;
#if ANDROID
            var h = new Xamarin.Android.Net.AndroidMessageHandler();
            h.ServerCertificateCustomValidationCallback =
                (_, cert, _, errors) =>
                    cert?.Issuer == "CN=localhost" ||
                    errors == System.Net.Security.SslPolicyErrors.None;
            handler = h;
#else
            handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };
#endif
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(BaseUrl),
                Timeout = Timeout.InfiniteTimeSpan
            };
        }
    }
}

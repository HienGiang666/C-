using System.Diagnostics;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        // IP WiFi hiện tại của máy dev — cập nhật nếu đổi mạng
        private const string DefaultUrl = "http://192.168.1.5:5254";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly object ClientLock = new();
        private static HttpClient? _client;

        public static string BaseUrl
        {
            get => Preferences.Default.Get("api_base_url", DefaultUrl);
            set
            {
                var normalized = value.TrimEnd('/');
                Preferences.Default.Set("api_base_url", normalized);
                // Recreate client with new base address to avoid stale DNS/port
                lock (ClientLock)
                {
                    _client?.Dispose();
                    _client = CreateClient(normalized);
                }
            }
        }

        public static void UpdateBaseUrl(string newUrl) => BaseUrl = newUrl;

        [DebuggerNonUserCode]
        public static async Task AutoDiscoverApiAsync()
        {
            // Timeout 5s cho toàn bộ discovery để tránh treo login
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try
            {
                await AutoDiscoverInternalAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[ApiService] Auto-Discovery timeout (5s), using default URL");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Auto-Discovery error: {ex.Message}");
            }
        }

        [DebuggerNonUserCode]
        private static async Task AutoDiscoverInternalAsync(CancellationToken ct)
        {
            Debug.WriteLine("[ApiService] Start Auto Discovery...");
            var subnet = GetLocalSubnet();
            var ipsToTest = new List<string>();

            // 1. Emulator ưu tiên hàng đầu, không check DefaultUrl trước chặn đường
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
            {
                ipsToTest.Add("http://10.0.2.2:5254");
                ipsToTest.Add("http://10.0.2.2:7244");
            }
            else
            {
                ipsToTest.Add(DefaultUrl);
                if (BaseUrl != DefaultUrl && !string.IsNullOrWhiteSpace(BaseUrl))
                {
                    ipsToTest.Add(BaseUrl);
                }
            }

            // 2. Local network 
            for (int i = 1; i <= 10; i++)
            {
                ipsToTest.Add($"http://{subnet}.{i}:5254");
            }

            // Batch testing (hỗn hợp 5 url cùng lúc)
            for (int i = 0; i < ipsToTest.Count; i += 5)
            {
                ct.ThrowIfCancellationRequested();
                
                var batch = ipsToTest.Skip(i).Take(5).Select(url => TestUrlQuickAsync(url, ct)).ToList();
                
                while (batch.Count > 0)
                {
                    var finished = await Task.WhenAny(batch);
                    batch.Remove(finished);
                    var res = await finished;
                    if (res != null)
                    {
                        BaseUrl = res;
                        Debug.WriteLine($"[ApiService] Auto-Discovery SUCCESS: {res}");
                        return;
                    }
                }
            }
        }

        [DebuggerNonUserCode]
        private static async Task<string?> TestUrlQuickAsync(string url, CancellationToken ct)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(1.5) };
                var r = await client.GetAsync($"{url}/api/poi", ct);
                if (r.IsSuccessStatusCode) return url;
            }
            catch { }
            return null;
        }

        [DebuggerNonUserCode]
        private static async Task<string?> TestUrlAsync(string url)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2.5) };
                var r = await client.GetAsync($"{url}/api/poi");
                if (r.IsSuccessStatusCode) return url;
            }
            catch { }
            return null;
        }

        private static string GetLocalSubnet()
        {
            try {
                foreach (var netInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        foreach (var addrInfo in netInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (addrInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                var ip = addrInfo.Address.ToString();
                                if (ip.StartsWith("192.168.") || ip.StartsWith("10.") || ip.StartsWith("172."))
                                {
                                    return ip.Substring(0, ip.LastIndexOf('.'));
                                }
                            }
                        }
                    }
                }
            } catch { }
            return "192.168.1";
        }

        [DebuggerNonUserCode]
        public Task<bool> TestConnectionAsync() =>
            TryFetch("/api/poi", _ => true, () => false);

        [DebuggerNonUserCode]
        public async Task<List<POI>> GetAllPOIsAsync()
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, "pois.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync("/api/poi?approvedOnly=true", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<POI>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetAllPOIsAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<POI>>(cached, JsonOpts) ?? new();
            }
            return new List<POI>();
        }

        [DebuggerNonUserCode]
        public Task<List<Tour>> GetAllToursAsync() =>
            TryFetch(
                "/api/tour",
                body => JsonSerializer.Deserialize<List<Tour>>(body, JsonOpts) ?? new(),
                () => new List<Tour>()
            );

        [DebuggerNonUserCode]
        public Task<Tour?> GetTourByIdAsync(int tourId) =>
            TryFetch(
                $"/api/tour/{tourId}",
                body => JsonSerializer.Deserialize<Tour>(body, JsonOpts),
                () => null
            );

        [DebuggerNonUserCode]
        public Task<List<TourPOI>> GetTourStopsAsync(int tourId) =>
            TryFetch(
                $"/api/tour/{tourId}/stops",
                body => JsonSerializer.Deserialize<List<TourPOI>>(body, JsonOpts) ?? new(),
                () => new List<TourPOI>()
            );

        public async Task<(bool Success, string Message)> BookTourAsync(Booking booking)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var json = JsonSerializer.Serialize(booking);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await GetClient().PostAsync("/api/booking", content, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    return (true, "Đặt tour thành công!");
                }
                
                var errorMsg = await response.Content.ReadAsStringAsync();
                return (false, $"Lỗi server: {errorMsg}");
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi kết nối mạng: {ex.Message}");
            }
        }

        [DebuggerNonUserCode]
        public Task<List<Language>> GetLanguagesAsync() =>
            TryFetch(
                "/api/language",
                body => JsonSerializer.Deserialize<List<Language>>(body, JsonOpts) ?? new(),
                () => new List<Language>()
            );

        public Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi") =>
            TryFetch(
                $"/api/audio/poi/{poiId}?lang={lang}",
                body => JsonSerializer.Deserialize<Audio>(body, JsonOpts),
                () => null
            );

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            try
            {
                string deviceId = "Unknown";
                try { deviceId = DeviceInfo.Current.Platform + "-" + DeviceInfo.Current.Idiom; } catch { }

                var payload = new
                {
                    POIId = poiId,
                    AudioId = audioId,
                    TriggerType = triggerType,
                    DeviceId = deviceId
                };

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await GetClient().PostAsync("/api/NarrationLog", content, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[ApiService] LogNarration [OK] Type: {triggerType}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] LogNarration error: {ex.Message}");
            }
        }

        [DebuggerNonUserCode]
        private static async Task<T> TryFetch<T>(
            string path,
            Func<string, T> parse,
            Func<T> fallback)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync(path, cts.Token).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[ApiService] {path}: HTTP {(int)response.StatusCode}");
                    return fallback();
                }

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return parse(body);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine($"[ApiService] {path}: timeout");
                return fallback();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] {path}: {ex.Message}");
                return fallback();
            }
        }

        [DebuggerNonUserCode]
        private static HttpClient GetClient()
        {
            lock (ClientLock)
            {
                return _client ??= CreateClient(BaseUrl);
            }
        }

        private static HttpClient CreateClient(string baseUrl)
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
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(8) // Oppo A31 dễ treo nếu timeout vô hạn
            };
        }
    }
}

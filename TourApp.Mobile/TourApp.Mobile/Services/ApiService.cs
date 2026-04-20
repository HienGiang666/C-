using System.Diagnostics;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        // IP WiFi hiện tại của máy dev — cập nhật nếu đổi mạng
        private const string DefaultUrl = "http://10.89.192.150:5254";

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
            var myIp = GetLocalIpAddress();
            Debug.WriteLine($"[ApiService] Start Auto Discovery... (This device IP: {myIp ?? "Unknown"})");
            Debug.WriteLine($"[ApiService] Looking for API server in local network...");
            
            var subnet = GetLocalSubnet();
            var ipsToTest = new List<string>();

            // 1. Emulator ưu tiên hàng đầu
            if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
            {
                ipsToTest.Add("http://10.0.2.2:5254");
                ipsToTest.Add("http://10.0.2.2:7244");
            }
            else
            {
                // Phone thật: ưu tiên DefaultUrl và BaseUrl trước
                ipsToTest.Add(DefaultUrl);
                Debug.WriteLine($"[ApiService] Will try DefaultUrl: {DefaultUrl}");
                
                if (BaseUrl != DefaultUrl && !string.IsNullOrWhiteSpace(BaseUrl))
                {
                    ipsToTest.Add(BaseUrl);
                    Debug.WriteLine($"[ApiService] Will try saved BaseUrl: {BaseUrl}");
                }
            }

            // 2. Quét subnet rộng hơn (1-50 trước, nếu không thấy thì quét tiếp)
            Debug.WriteLine($"[ApiService] Scanning subnet {subnet}.1-50...");
            for (int i = 1; i <= 50; i++)
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
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
                var r = await client.GetAsync($"{url}/api/poi", ct);
                if (r.IsSuccessStatusCode) 
                {
                    Debug.WriteLine($"[ApiService] Found server at: {url}");
                    return url;
                }
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
        public async Task<List<Tour>> GetAllToursAsync()
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, "tours.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync("/api/tour", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<Tour>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetAllToursAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<Tour>>(cached, JsonOpts) ?? new();
            }
            return new List<Tour>();
        }

        [DebuggerNonUserCode]
        public async Task<Tour?> GetTourByIdAsync(int tourId)
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, $"tour_{tourId}.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/tour/{tourId}", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<Tour>(body, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetTourByIdAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<Tour>(cached, JsonOpts);
            }
            return null;
        }

        [DebuggerNonUserCode]
        public async Task<List<TourPOI>> GetTourStopsAsync(int tourId)
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, $"tour_stops_{tourId}.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/tour/{tourId}/stops", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<TourPOI>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetTourStopsAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<TourPOI>>(cached, JsonOpts) ?? new();
            }
            return new List<TourPOI>();
        }

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
        public async Task<List<Language>> GetLanguagesAsync()
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, "languages.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync("/api/language", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<Language>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetLanguagesAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<Language>>(cached, JsonOpts) ?? new();
            }
            return new List<Language>();
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, $"audio_{poiId}_{lang}.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/audio/poi/{poiId}?lang={lang}", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<Audio>(body, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetAudioByPoiAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<Audio>(cached, JsonOpts);
            }
            return null;
        }

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
        // ===== LANGUAGE / TRANSLATION SYNC =====

        /// <summary>
        /// Tải toàn bộ bản dịch UI từ server, cache vào file JSON.
        /// </summary>
        [DebuggerNonUserCode]
        public async Task<Dictionary<string, Dictionary<string, string>>?> GetUiTranslationsAsync()
        {
            var cacheFile = Path.Combine(FileSystem.CacheDirectory, "ui_translations.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync("/api/language/translations", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(body, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] GetUiTranslationsAsync failed: {ex.Message}");
            }

            // Fallback to cached file
            if (File.Exists(cacheFile))
            {
                try
                {
                    var cached = await File.ReadAllTextAsync(cacheFile);
                    return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(cached, JsonOpts);
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// Lấy IP WiFi hiện tại của máy tính để hiển thị cho user
        /// </summary>
        public static string? GetLocalIpAddress()
        {
            try {
                foreach (var netInterface in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (netInterface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        netInterface.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    {
                        foreach (var addrInfo in netInterface.GetIPProperties().UnicastAddresses)
                        {
                            if (addrInfo.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            {
                                var ip = addrInfo.Address.ToString();
                                if (ip.StartsWith("192.168.") || ip.StartsWith("10.0.") || ip.StartsWith("172."))
                                {
                                    return ip;
                                }
                            }
                        }
                    }
                }
            } catch { }
            return null;
        }
    }
}

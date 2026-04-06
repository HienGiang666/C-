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

        [DebuggerNonUserCode]
        public static async Task AutoDiscoverApiAsync()
        {
            try
            {
                // Nếu URL hiện tại còn sống thì không cần quét
                if (await new ApiService().TestConnectionAsync()) return;

                Debug.WriteLine("[ApiService] Start Auto Discovery...");
                var subnet = GetLocalSubnet();
                var ipsToTest = new List<string>();
                
                // 1. Emulator
                if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
                {
                    ipsToTest.Add("http://10.0.2.2:5254");
                    ipsToTest.Add("http://10.0.2.2:7244");
                }

                // 2. Local network common IPs (1-20 instead of 254 to prevent ANR freeze) 
                for (int i = 1; i <= 20; i++)
                {
                    ipsToTest.Add($"http://{subnet}.{i}:5254");
                }
                
                // Also add default fallback
                ipsToTest.Add(DefaultUrl);

                // Batch testing (5 at a time max) to avoid socket exhaustion
                for (int i = 0; i < ipsToTest.Count; i += 5)
                {
                    var batch = ipsToTest.Skip(i).Take(5).Select(url => TestUrlAsync(url)).ToList();
                    
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
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Auto-Discovery FAIL: {ex.Message}");
            }
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

        public Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            return Task.Run(async () =>
            {
                try
                {
                    string deviceId = "Unknown";
                    try { deviceId = DeviceInfo.Current.Platform.ToString() + "-" + DeviceInfo.Current.Idiom.ToString(); } catch { }

                    using var client = CreateClient();
                    var payload = new
                    {
                        POIId = poiId,
                        AudioId = audioId,
                        TriggerType = triggerType,
                        DeviceId = deviceId
                    };
                    var json = JsonSerializer.Serialize(payload);
                    var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                    var postTask = client.PostAsync("/api/NarrationLog", content);
                    var winner = await Task.WhenAny(postTask, Task.Delay(5000));
                    
                    if (winner == postTask && postTask.IsCompletedSuccessfully)
                    {
                        var response = postTask.Result;
                        if (response.IsSuccessStatusCode)
                        {
                            Debug.WriteLine($"[ApiService] LogNarration [OK] Type: {triggerType}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ApiService] LogNarration error: {ex.Message}");
                }
            });
        }

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

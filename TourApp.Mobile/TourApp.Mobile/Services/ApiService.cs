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

        public static async Task AutoDiscoverApiAsync()
        {
            try
            {
                // Nếu URL hiện tại còn sống thì không cần quét
                if (await new ApiService().TestConnectionAsync()) return;

                Debug.WriteLine("[ApiService] Start Auto Discovery...");
                var subnet = GetLocalSubnet();
                var tasks = new List<Task<string?>>();

                // Try emulator standard IP first if virtual
                if (DeviceInfo.Platform == DevicePlatform.Android && DeviceInfo.DeviceType == DeviceType.Virtual)
                {
                    tasks.Add(TestUrlAsync("http://10.0.2.2:5254"));
                }

                // Scan local network x.x.x.1 to x.x.x.254
                for (int i = 1; i <= 254; i++)
                {
                    string url = $"http://{subnet}.{i}:5254";
                    tasks.Add(TestUrlAsync(url));
                }

                // Also scan common local if different
                if (subnet != "192.168.1")
                {
                    for (int i = 1; i <= 254; i++)
                    {
                        string url = $"http://192.168.1.{i}:5254";
                        tasks.Add(TestUrlAsync(url));
                    }
                }

                while (tasks.Count > 0)
                {
                    var finished = await Task.WhenAny(tasks);
                    tasks.Remove(finished);
                    var res = await finished;
                    if (res != null)
                    {
                        BaseUrl = res;
                        Debug.WriteLine($"[ApiService] Auto-Discovery SUCCESS: {res}");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Auto-Discovery FAIL: {ex.Message}");
            }
        }

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

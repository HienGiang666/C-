using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        // ── Constants ────────────────────────────────────────────────────
        public const string PrefsKey = "api_base_url";
        private const int ApiPort = 5254;

        // ── Shared HttpClient (reuse, không tạo mới mỗi lần) ─────────────
        private static readonly HttpClient _sharedHttp = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        // ── State ─────────────────────────────────────────────────────────
        private string _baseUrl;
        private Task? _discoveryTask;

        // ─────────────────────────────────────────────────────────────────
        public ApiService()
        {
            _baseUrl = Preferences.Default.Get(PrefsKey, "");
            // Bắt đầu quét mạng ngầm ngay lập tức
            _discoveryTask = EnsureConnectedAsync();
        }

        // ── Public interface ──────────────────────────────────────────────
        public string GetBaseUrl() => _baseUrl;

        public void SetBaseUrl(string url)
        {
            _baseUrl = url.TrimEnd('/');
            Preferences.Default.Set(PrefsKey, _baseUrl);
            System.Diagnostics.Debug.WriteLine($"[API] URL → {_baseUrl}");
        }

        // ── Connectivity ──────────────────────────────────────────────────

        public Task EnsureConnectedAsync()
        {
            // Nếu có task đang chạy, trả về task đó để cùng chờ
            if (_discoveryTask != null && !_discoveryTask.IsCompleted)
            {
                return _discoveryTask;
            }

            _discoveryTask = PerformDiscoveryAsync();
            return _discoveryTask;
        }

        private async Task PerformDiscoveryAsync()
        {
            try
            {
                // 1. Thử URL đã lưu
                if (!string.IsNullOrEmpty(_baseUrl) && await QuickTestAsync(_baseUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"[API] Connected: {_baseUrl}");
                    return;
                }

                // 2. Quét mạng
                System.Diagnostics.Debug.WriteLine("[API] Scanning LAN...");
                var found = await DiscoverAsync();
                if (found != null) SetBaseUrl(found);
                else System.Diagnostics.Debug.WriteLine("[API] No server found.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] EnsureConnected error: {ex.Message}");
            }
        }

        // ── Auto-Discovery ────────────────────────────────────────────────

        /// <summary>
        /// Quét mạng LAN để tìm API server.
        /// Dùng TCP (nhanh, ít tốn RAM) + giới hạn 25 concurrent connections.
        /// </summary>
        private static async Task<string?> DiscoverAsync()
        {
            try
            {
                var localIP = GetLocalIP();
                System.Diagnostics.Debug.WriteLine($"[API] Local IP: {localIP}");

                if (string.IsNullOrEmpty(localIP)) return null;

                var parts = localIP.Split('.');
                if (parts.Length != 4) return null;

                var subnet = $"{parts[0]}.{parts[1]}.{parts[2]}.";
                var ownOctet = parts[3];

                // Giới hạn 25 connections đồng thời (tránh OOM trên thiết bị RAM thấp)
                using var semaphore = new SemaphoreSlim(25, 25);

                var tasks = Enumerable.Range(1, 254)
                    .Where(i => i.ToString() != ownOctet)
                    .Select(i => ProbeHostAsync(subnet + i, ApiPort, semaphore));

                var results = await Task.WhenAll(tasks);
                return results.FirstOrDefault(r => r != null);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] Discover error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Kiểm tra một host: TCP connect trước (rất nhanh, không tốn RAM),
        /// chỉ khi có TCP mới gọi HTTP để verify.
        /// </summary>
        private static async Task<string?> ProbeHostAsync(string ip, int port, SemaphoreSlim sem)
        {
            await sem.WaitAsync();
            try
            {
                // TCP connect với timeout 300ms
                if (!await TcpConnectAsync(ip, port, 300)) return null;

                // Verify bằng HTTP
                var url = $"http://{ip}:{port}";
                return await QuickTestAsync(url, 1500) ? url : null;
            }
            catch { return null; }
            finally { sem.Release(); }
        }

        private static async Task<bool> TcpConnectAsync(string ip, int port, int timeoutMs)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(ip, port, cts.Token);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<bool> QuickTestAsync(string baseUrl, int timeoutMs = 2500)
        {
            try
            {
                using var cts = new CancellationTokenSource(timeoutMs);
                // Dùng _sharedHttp nhưng với CancellationToken để timeout
                var response = await _sharedHttp.GetAsync($"{baseUrl}/api/poi", cts.Token);
                return response.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// Lấy IP WiFi của thiết bị (ưu tiên 192.168.x.x).
        /// Dùng NetworkInterface (đáng tin hơn Dns.GetHostAddresses trên Android).
        /// </summary>
        private static string? GetLocalIP()
        {
            try
            {
                // Cách 1: NetworkInterface (đáng tin)
                var ip = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                                 && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
                    .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork
                                && !IPAddress.IsLoopback(a.Address))
                    .OrderByDescending(a => a.Address.ToString().StartsWith("192.168."))
                    .Select(a => a.Address.ToString())
                    .FirstOrDefault();

                if (!string.IsNullOrEmpty(ip)) return ip;

                // Cách 2: Fallback qua DNS
                return Dns.GetHostAddresses(Dns.GetHostName())
                    .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork
                                        && !IPAddress.IsLoopback(a))
                    ?.ToString();
            }
            catch { return null; }
        }

        // ── API Methods ───────────────────────────────────────────────────

        public async Task<bool> TestConnectionAsync()
        {
            if (string.IsNullOrEmpty(_baseUrl)) return false;
            return await QuickTestAsync(_baseUrl, 3000);
        }

        public async Task<List<POI>> GetAllPOIsAsync()
        {
            if (string.IsNullOrEmpty(_baseUrl))
            {
                await EnsureConnectedAsync();
                if (string.IsNullOrEmpty(_baseUrl)) return new List<POI>();
            }

            try
            {
                var response = await _sharedHttp.GetAsync($"{_baseUrl}/api/poi");
                if (!response.IsSuccessStatusCode) return new List<POI>();

                var json = await response.Content.ReadAsStringAsync();
                var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<List<POI>>(json, opts) ?? new List<POI>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API] GetPOIs error: {ex.Message}");
                return new List<POI>();
            }
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            await Task.CompletedTask;
            return null;
        }

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            await Task.CompletedTask;
        }
    }
}

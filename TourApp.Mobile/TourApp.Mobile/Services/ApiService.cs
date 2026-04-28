using System.Diagnostics;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        // IP WiFi hiện tại của máy dev — cập nhật nếu đổi mạng
        private const string DefaultUrl = "https://hypocrite-ground-tackle.ngrok-free.dev";

        /// <summary>Kiểm tra mạng nhanh trước khi gọi API</summary>
        public static bool IsOnline => NetworkService.IsConnected;

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
            // Bỏ qua discovery khi offline — dùng URL đã lưu hoặc default
            if (!IsOnline)
            {
                Debug.WriteLine("[ApiService] Auto-Discovery skipped — offline");
                return;
            }

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
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, "pois.json");
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
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, "tours.json");
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
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, $"tour_{tourId}.json");
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
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, $"tour_stops_{tourId}.json");
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
            // Offline → queue lại, sync khi có mạng
            if (!IsOnline)
            {
                await OfflineQueueService.EnqueueAsync(new OfflineAction
                {
                    Type = OfflineActionType.Booking,
                    Payload = JsonSerializer.Serialize(booking)
                });
                return (true, "Đã lưu đặt tour. Sẽ gửi lên server khi có mạng.");
            }

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
            catch (Exception)
            {
                // Mạng lỗi giữa chừng → queue offline
                await OfflineQueueService.EnqueueAsync(new OfflineAction
                {
                    Type = OfflineActionType.Booking,
                    Payload = JsonSerializer.Serialize(booking)
                });
                return (true, $"Lỗi mạng. Đã lưu offline, sẽ sync khi có mạng.");
            }
        }

        /// <summary>Lấy danh sách booking của user (có cache offline)</summary>
        public async Task<List<Booking>> GetUserBookingsAsync(int userId)
        {
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, $"bookings_{userId}.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/booking/user/{userId}", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<Booking>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetUserBookingsAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<Booking>>(cached, JsonOpts) ?? new();
            }
            return new List<Booking>();
        }

        /// <summary>Lấy profile user (có cache offline)</summary>
        public async Task<User?> GetUserProfileAsync(int userId)
        {
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, $"profile_{userId}.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/user/{userId}", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<User>(body, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetUserProfileAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<User>(cached, JsonOpts);
            }
            return null;
        }

        [DebuggerNonUserCode]
        public async Task<List<Language>> GetLanguagesAsync()
        {
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, "languages.json");
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
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, $"audio_{poiId}_{lang}.json");
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

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType, string? deviceId = null)
        {
            deviceId ??= GetStableDeviceId();

            // Offline → queue
            if (!IsOnline)
            {
                await OfflineQueueService.EnqueueAsync(new OfflineAction
                {
                    Type = OfflineActionType.NarrationLog,
                    Payload = JsonSerializer.Serialize(new NarrationLogPayload
                    {
                        PoiId = poiId, AudioId = audioId, TriggerType = triggerType, DeviceId = deviceId
                    })
                });
                Debug.WriteLine($"[ApiService] LogNarration queued offline: {triggerType}");
                return;
            }

            try
            {
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
                // Mạng lỗi giữa chừng → queue
                await OfflineQueueService.EnqueueAsync(new OfflineAction
                {
                    Type = OfflineActionType.NarrationLog,
                    Payload = JsonSerializer.Serialize(new NarrationLogPayload
                    {
                        PoiId = poiId, AudioId = audioId, TriggerType = triggerType, DeviceId = deviceId
                    })
                });
                Debug.WriteLine($"[ApiService] LogNarration error, queued offline: {ex.Message}");
            }
        }

        /// <summary>Trả về device ID nhất quán giữa location và narration logs.</summary>
        public static string GetStableDeviceId()
        {
            var name = DeviceInfo.Name;
            if (!string.IsNullOrEmpty(name))
                return name;
            // Emulator fallback: ưu tiên device model, nếu không có thì platform
            try
            {
                var model = DeviceInfo.Current.Model;
                if (!string.IsNullOrEmpty(model))
                    return $"emu_{model}";
            }
            catch { }
            return $"emu_{DeviceInfo.Current.Platform}";
        }

        [DebuggerNonUserCode]
        private static async Task<T> TryFetch<T>(
            string path,
            Func<string, T> parse,
            Func<T> fallback,
            int maxRetries = 2) where T : notnull
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5 + attempt * 2)); // Increase timeout per retry
                    var response = await GetClient().GetAsync(path, cts.Token).ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                        return parse(body);
                    }
                    
                    // Retry on 5xx errors or 429 (too many requests)
                    if ((int)response.StatusCode >= 500 || (int)response.StatusCode == 429)
                    {
                        if (attempt < maxRetries - 1)
                        {
                            var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)); // Exponential backoff: 100ms, 200ms
                            Debug.WriteLine($"[ApiService] {path}: HTTP {(int)response.StatusCode}, retrying in {delay.TotalMilliseconds}ms...");
                            await Task.Delay(delay);
                            continue;
                        }
                    }
                    
                    Debug.WriteLine($"[ApiService] {path}: HTTP {(int)response.StatusCode}");
                    return fallback();
                }
                catch (OperationCanceledException)
                {
                    if (attempt < maxRetries - 1)
                    {
                        Debug.WriteLine($"[ApiService] {path}: timeout, retrying...");
                        continue;
                    }
                    Debug.WriteLine($"[ApiService] {path}: timeout after {maxRetries} attempts");
                    return fallback();
                }
                catch (HttpRequestException ex) when (attempt < maxRetries - 1)
                {
                    var delay = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt));
                    Debug.WriteLine($"[ApiService] {path}: network error (attempt {attempt + 1}), retrying in {delay.TotalMilliseconds}ms... Error: {ex.Message}");
                    await Task.Delay(delay);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ApiService] {path}: {ex.Message}");
                    return fallback();
                }
            }
            
            return fallback();
        }

        /// <summary>
        /// Execute a function with retry logic using exponential backoff
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3) where T : notnull
        {
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxRetries - 1 && IsRetryableException(ex))
                {
                    var delay = TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt)); // 200ms, 400ms, 800ms
                    Debug.WriteLine($"[ApiService] Operation failed (attempt {attempt + 1}), retrying in {delay.TotalMilliseconds}ms...");
                    await Task.Delay(delay);
                }
            }
            
            // Last attempt - let it throw if it fails
            return await operation();
        }

        private static bool IsRetryableException(Exception ex)
        {
            return ex is HttpRequestException || 
                   ex is OperationCanceledException ||
                   ex is TimeoutException ||
                   (ex is System.Net.Sockets.SocketException socketEx && 
                    (socketEx.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut ||
                     socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused));
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
            // Enable automatic decompression and connection pooling
            h.AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate;
            handler = h;
#else
#if WINDOWS
            var winHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            handler = winHandler;
#else
            var socketHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                MaxConnectionsPerServer = 10,
                EnableMultipleHttp2Connections = true,
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
            };
            handler = socketHandler;
#endif
#endif
            var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(baseUrl),
                Timeout = TimeSpan.FromSeconds(15) // Increased timeout for slower networks
            };
            
            // Add default headers
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");
            
            // Add JWT Authorization header if token exists
            var authToken = Preferences.Default.Get("auth_token", string.Empty);
            if (!string.IsNullOrEmpty(authToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authToken);
            }
            
            return client;
        }
        // ===== LANGUAGE / TRANSLATION SYNC =====

        /// <summary>
        /// Tải toàn bộ bản dịch UI từ server, cache vào file JSON.
        /// </summary>
        [DebuggerNonUserCode]
        public async Task<Dictionary<string, Dictionary<string, string>>?> GetUiTranslationsAsync()
        {
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, "ui_translations.json");
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

        #region Payment Methods (Migration v2)

        /// <summary>Verify QR payment - Giả lập thanh toán thành công</summary>
        public async Task<(bool Success, string Message, string? TransactionId)> VerifyQrPaymentAsync(int bookingId)
        {
            try
            {
                var qrData = QrCodeService.CreatePaymentQrData(bookingId, 0, "");
                var request = new { BookingId = bookingId, QrData = qrData };
                
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await GetClient().PostAsync("/api/payment/verify-qr", content, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;
                    var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Thành công";
                    var transactionId = root.TryGetProperty("transactionId", out var transProp) ? transProp.GetString() : null;
                    return (true, message ?? "Thành công", transactionId);
                }
                
                return (false, $"Lỗi: {responseBody}", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Payment error: {ex.Message}");
                return (false, "Lỗi kết nối. Vui lòng thử lại.", null);
            }
        }

        /// <summary>Cancel booking with reason</summary>
        public async Task<(bool Success, string Message)> CancelBookingAsync(int bookingId, string reason)
        {
            try
            {
                var request = new { Reason = reason };
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var response = await GetClient().PostAsync($"/api/payment/{bookingId}/cancel", content, cts.Token);
                var responseBody = await response.Content.ReadAsStringAsync();
                
                if (response.IsSuccessStatusCode)
                    return (true, "Đã hủy booking thành công");
                
                return (false, $"Lỗi: {responseBody}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Cancel error: {ex.Message}");
                return (false, "Lỗi kết nối. Vui lòng thử lại.");
            }
        }

        /// <summary>Lấy lịch sử thanh toán của user</summary>
        public async Task<List<Payment>> GetUserPaymentsAsync(int userId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/payment/user/{userId}", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token);
                    return JsonSerializer.Deserialize<List<Payment>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Get payments error: {ex.Message}");
            }
            return new List<Payment>();
        }

        /// <summary>Lấy thông tin booking theo ID</summary>
        public async Task<Booking?> GetBookingAsync(int bookingId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/booking/{bookingId}", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token);
                    return JsonSerializer.Deserialize<Booking>(body, JsonOpts);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Get booking error: {ex.Message}");
            }
            return null;
        }

        #endregion

        #region Category Methods (Migration v2)

        /// <summary>Lấy tất cả danh mục POI</summary>
        public async Task<List<Category>> GetCategoriesAsync()
        {
            var cacheFile = Path.Combine(FileSystem.AppDataDirectory, "categories.json");
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync("/api/category", cts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                    await File.WriteAllTextAsync(cacheFile, body);
                    return JsonSerializer.Deserialize<List<Category>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Fallback to cache for GetCategoriesAsync: {ex.Message}");
            }

            if (File.Exists(cacheFile))
            {
                var cached = await File.ReadAllTextAsync(cacheFile);
                return JsonSerializer.Deserialize<List<Category>>(cached, JsonOpts) ?? new();
            }
            return new List<Category>();
        }

        /// <summary>Lấy POI theo category</summary>
        public async Task<List<POI>> GetPOIsByCategoryAsync(int categoryId)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var response = await GetClient().GetAsync($"/api/category/{categoryId}/pois", cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(cts.Token);
                    return JsonSerializer.Deserialize<List<POI>>(body, JsonOpts) ?? new();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] Get POIs by category error: {ex.Message}");
            }
            return new List<POI>();
        }

        #endregion

        /// <summary>
        /// Lưu tuyến đường vừa chỉ đường lên server để thống kê chuyến đi phổ biến
        /// </summary>
        public async Task SaveRouteAsync(List<double[]> coordinates, string? deviceId = null, int? userId = null)
        {
            if (coordinates == null || coordinates.Count < 2) return;
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                var payload = new
                {
                    DeviceId = deviceId,
                    UserId = userId,
                    Coordinates = coordinates
                };
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                var response = await GetClient().PostAsync("/api/userlocation/save-route", content, cts.Token);
                if (response.IsSuccessStatusCode)
                    Debug.WriteLine("[ApiService] Route saved successfully");
                else
                    Debug.WriteLine($"[ApiService] SaveRoute failed: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApiService] SaveRoute error: {ex.Message}");
            }
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

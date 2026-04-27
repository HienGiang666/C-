using System.Diagnostics;
using System.Net.Http.Json;

namespace TourApp.Mobile.Services
{
    /// <summary>
    /// Service quản lý user session - báo server khi user online/offline
    /// </summary>
    public static class UserSessionService
    {
        private static CancellationTokenSource? _heartbeatCts;
        private static int? _currentUserId;
        private static string? _currentGuestId;

        /// <summary>
        /// Bắt đầu session tracking cho user đăng nhập hoặc khách
        /// </summary>
        /// <param name="userId">ID user (null nếu là khách)</param>
        /// <param name="name">Tên hiển thị</param>
        /// <param name="guestId">ID khách (nếu là khách)</param>
        public static void StartSession(int? userId, string name, string? guestId = null)
        {
            try
            {
                _currentUserId = userId;
                _currentGuestId = guestId;

                // Dừng heartbeat cũ nếu có
                _heartbeatCts?.Cancel();
                _heartbeatCts = new CancellationTokenSource();

                // Gọi API báo online ngay lập tức (fire and forget)
                _ = Task.Run(async () => await SendSessionAsync(true, name));

                // Bắt đầu heartbeat mỗi 5 giây (real-time cho CMS)
                _ = Task.Run(async () => await RunHeartbeatAsync(name, _heartbeatCts.Token));

                Debug.WriteLine($"[UserSessionService] Session started - UserId: {userId}, GuestId: {guestId}, Name: {name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSessionService] StartSession error: {ex.Message}");
            }
        }

        /// <summary>
        /// Dừng session và báo server user đã offline
        /// </summary>
        public static async Task StopSessionAsync()
        {
            try
            {
                _heartbeatCts?.Cancel();
                await SendSessionAsync(false, null);
                Debug.WriteLine("[UserSessionService] Session stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSessionService] StopSession error: {ex.Message}");
            }
        }

        private static async Task RunHeartbeatAsync(string name, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    if (!ct.IsCancellationRequested)
                        await SendSessionAsync(true, name);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[UserSessionService] Heartbeat error: {ex.Message}");
                }
            }
        }

        private static async Task SendSessionAsync(bool isOnline, string? name)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

                // Kiểm tra mock mode từ LocationService
                var isMocking = LocationService.Current?.IsMocking == true;
                var mockLoc = LocationService.Current?.MockLocation;

                // Lấy GPS hiện tại để gửi kèm heartbeat
                double lat = 0, lng = 0;
                bool isMock = false;

                if (isMocking && mockLoc != null)
                {
                    // Đang mock → gửi vị trí giả lập
                    lat = mockLoc.Latitude;
                    lng = mockLoc.Longitude;
                    isMock = true;
                }
                else
                {
                    try
                    {
                        var location = await Geolocation.GetLastKnownLocationAsync();
                        if (location != null)
                        {
                            lat = location.Latitude;
                            lng = location.Longitude;
                        }
                    }
                    catch { }
                }

                using var client = new HttpClient { BaseAddress = new Uri(baseUrl) };
                client.Timeout = TimeSpan.FromSeconds(5);

                var request = new
                {
                    UserId = _currentUserId,
                    GuestId = _currentGuestId,
                    IsOnline = isOnline,
                    Name = name,
                    DeviceInfo = DeviceInfo.Name,
                    Platform = DeviceInfo.Platform.ToString(),
                    Version = DeviceInfo.Version.ToString(),
                    Latitude = lat,
                    Longitude = lng,
                    Timestamp = DateTime.UtcNow,
                    IsMock = isMock
                };

                var response = await client.PostAsJsonAsync("/api/userlocation/session", request);
                if (response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[UserSessionService] Session update sent: IsOnline={isOnline}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserSessionService] SendSession error: {ex.Message}");
            }
        }
    }
}

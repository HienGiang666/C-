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
        private static double? _currentLatitude;
        private static double? _currentLongitude;

        /// <summary>
        /// Bắt đầu session tracking cho user đăng nhập hoặc khách
        /// </summary>
        /// <param name="userId">ID user (null nếu là khách)</param>
        /// <param name="name">Tên hiển thị</param>
        /// <param name="guestId">ID khách (nếu là khách)</param>
        public static void StartSession(int? userId, string name, string? guestId = null, double? latitude = null, double? longitude = null)
        {
            try
            {
                _currentUserId = userId;
                _currentGuestId = guestId;
                _currentLatitude = latitude;
                _currentLongitude = longitude;

                // Dừng heartbeat cũ nếu có
                _heartbeatCts?.Cancel();
                _heartbeatCts = new CancellationTokenSource();

                // Gọi API báo online ngay lập tức (fire and forget)
                _ = Task.Run(async () => await SendSessionAsync(true, name));

                // Bắt đầu heartbeat mỗi 5 giây để cập nhật online nhanh hơn
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

        /// <summary>
        /// Cập nhật vị trí hiện tại mà không khởi động lại session/heartbeat
        /// </summary>
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

        public static void UpdateLocation(double latitude, double longitude)
        {
            _currentLatitude = latitude;
            _currentLongitude = longitude;
        }

        private static async Task SendSessionAsync(bool isOnline, string? name)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

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
                    Latitude = _currentLatitude,
                    Longitude = _currentLongitude,
                    Timestamp = DateTime.UtcNow
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

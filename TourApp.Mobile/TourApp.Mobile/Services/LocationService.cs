using Microsoft.Maui.Devices.Sensors;
using System.Net.Http.Json;

namespace TourApp.Mobile.Services
{
    /// <summary>
    /// LocationService - GPS tracking with Background support
    /// - Foreground: Uses Geolocation.Default.GetLocationAsync()
    /// - Background: Continues tracking even when app is minimized
    /// - Permissions: LocationAlways for background, LocationWhenInUse for foreground
    /// </summary>
    public class LocationService
    {
        public static LocationService? Current { get; private set; }

        public event EventHandler<Location>? LocationChanged;
        private bool _isTracking = false;
        private readonly object _trackingLock = new();
        private CancellationTokenSource? _trackingCts;
        private DateTime _lastApiLogTime = DateTime.MinValue;

        public bool IsMocking { get; set; } = false;
        public Location? MockLocation { get; set; }

        public LocationService()
        {
            Current = this;
        }
        public bool IsTracking 
        { 
            get 
            { 
                lock (_trackingLock) return _isTracking; 
            } 
        }

        public async Task StartTracking()
        {
            lock (_trackingLock)
            {
                if (_isTracking) return;
                _isTracking = true;
            }

            try
            {
                // Request LocationAlways permission for background tracking
                var status = await RequestLocationPermissionsAsync();
                if (status != PermissionStatus.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("[LocationService] GPS permission denied.");
                    lock (_trackingLock) _isTracking = false;
                    return;
                }

                // Dispose old CTS if exists
                _trackingCts?.Cancel();
                _trackingCts?.Dispose();
                _trackingCts = new CancellationTokenSource();

                System.Diagnostics.Debug.WriteLine("[LocationService] Starting background GPS tracking...");
                RunGpsLoopSafe();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] StartTracking error: {ex}");
                lock (_trackingLock) _isTracking = false;
            }
        }

        /// <summary>
        /// Request LocationAlways permission (fallback to LocationWhenInUse)
        /// </summary>
        private async Task<PermissionStatus> RequestLocationPermissionsAsync()
        {
            PermissionStatus status;
            
            // First check LocationWhenInUse (required before requesting Always on iOS)
            if (MainThread.IsMainThread)
            {
                status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (status != PermissionStatus.Granted)
                {
                    status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
            }
            else
            {
                status = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var perm = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (perm != PermissionStatus.Granted)
                        perm = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    return perm;
                });
            }

            if (status != PermissionStatus.Granted)
            {
                System.Diagnostics.Debug.WriteLine("[LocationService] LocationWhenInUse not granted");
                return status;
            }

            // Then request LocationAlways for background tracking
            PermissionStatus alwaysStatus;
            if (MainThread.IsMainThread)
            {
                alwaysStatus = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                System.Diagnostics.Debug.WriteLine($"[LocationService] LocationAlways status: {alwaysStatus}");
                if (alwaysStatus != PermissionStatus.Granted)
                {
                    alwaysStatus = await Permissions.RequestAsync<Permissions.LocationAlways>();
                    System.Diagnostics.Debug.WriteLine($"[LocationService] LocationAlways after request: {alwaysStatus}");
                }
            }
            else
            {
                alwaysStatus = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var perm = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
                    System.Diagnostics.Debug.WriteLine($"[LocationService] LocationAlways status: {perm}");
                    if (perm != PermissionStatus.Granted)
                    {
                        perm = await Permissions.RequestAsync<Permissions.LocationAlways>();
                        System.Diagnostics.Debug.WriteLine($"[LocationService] LocationAlways after request: {perm}");
                    }
                    return perm;
                });
            }

            // Return Always status if granted, otherwise return WhenInUse status
            return alwaysStatus == PermissionStatus.Granted ? alwaysStatus : status;
        }

        /// <summary>
        /// Chạy GPS polling loop an toàn — mọi exception đều được bắt,
        /// không bao giờ làm crash app.
        /// Dùng cancellation token để dừng sạch sẽ.
        /// </summary>
        private void RunGpsLoopSafe()
        {
            Task.Run(async () =>
            {
                var cts = _trackingCts; // Capture reference locally
                var token = cts?.Token ?? CancellationToken.None;
                
                while (IsTracking && !token.IsCancellationRequested)
                {
                    try
                    {
                        Location? location = null;
                        
                        if (IsMocking && MockLocation != null)
                        {
                            location = MockLocation;
                        }
                        else
                        {
                            // Use Medium accuracy for better battery life
                            var request = new GeolocationRequest(
                                GeolocationAccuracy.Medium,
                                TimeSpan.FromSeconds(8)
                            );
                            
                            // Add cancellation token support with timeout
                            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(12));
                            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
                            location = await Geolocation.Default.GetLocationAsync(request, linkedCts.Token).ConfigureAwait(false);
                        }

                        if (location != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try 
                                { 
                                    LocationChanged?.Invoke(this, location);
                                    System.Diagnostics.Debug.WriteLine($"[LocationService] Location updated: {location.Latitude}, {location.Longitude}");
                                }
                                catch (Exception uiEx)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[LocationService] UI callback error: {uiEx.Message}");
                                }
                            });

                            // Auto-send location to API for CMS tracking (throttle 5s)
                            if ((DateTime.Now - _lastApiLogTime).TotalSeconds >= 5)
                            {
                                _lastApiLogTime = DateTime.Now;
                                _ = Task.Run(async () => await SendLocationToApiAsync(location));
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        System.Diagnostics.Debug.WriteLine("[LocationService] Tracking cancelled");
                        break;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocationService] GPS poll error: {ex.Message}");
                        // Không re-throw — loop tiếp tục chạy
                    }

                    // 8s polling for better battery life
                    try
                    {
                        await Task.Delay(8000, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                
                System.Diagnostics.Debug.WriteLine("[LocationService] GPS loop ended");
            }, _trackingCts?.Token ?? CancellationToken.None).ContinueWith(t =>
            {
                // Bắt mọi exception không được catch bên trong Task.Run
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"[LocationService] GPS loop faulted: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void StopTracking()
        {
            System.Diagnostics.Debug.WriteLine("[LocationService] Stopping tracking...");
            lock (_trackingLock)
            {
                _isTracking = false;
            }
            _trackingCts?.Cancel();
            _trackingCts?.Dispose();
            _trackingCts = null;
        }

        /// <summary>
        /// Check if app has location permissions
        /// </summary>
        public async Task<bool> HasLocationPermissionAsync()
        {
            var whenInUse = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            return whenInUse == PermissionStatus.Granted;
        }

        /// <summary>
        /// Check if app has background location permission
        /// </summary>
        public async Task<bool> HasBackgroundLocationPermissionAsync()
        {
            var always = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
            return always == PermissionStatus.Granted;
        }

        // --- Cross-platform foreground service stubs ---
#if ANDROID
        private bool _useAndroidForegroundService = false;

        public void SetUseForegroundService(bool use) => _useAndroidForegroundService = use;

        public async Task StartTrackingWithForegroundAsync()
        {
            if (_useAndroidForegroundService)
            {
                var locStatus = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                if (locStatus != PermissionStatus.Granted)
                    locStatus = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (locStatus != PermissionStatus.Granted)
                {
                    await StartTracking();
                    return;
                }
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
                {
                    var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                    if (notifStatus != PermissionStatus.Granted)
                        notifStatus = await Permissions.RequestAsync<Permissions.PostNotifications>();
                }
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context != null)
                {
                    TourApp.Mobile.Platforms.Android.Services.LocationForegroundService.Start(context);
                    return;
                }
            }
            await StartTracking();
        }

        public void StopTrackingWithForeground()
        {
            if (_useAndroidForegroundService)
            {
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context != null)
                    TourApp.Mobile.Platforms.Android.Services.LocationForegroundService.Stop(context);
            }
            StopTracking();
        }
#else
        public void SetUseForegroundService(bool use) { }
        public async Task StartTrackingWithForegroundAsync() => await StartTracking();
        public void StopTrackingWithForeground() => StopTracking();
#endif

        /// <summary>
        /// Gửi vị trí giả lập ngay lập tức đến API (dùng khi đặt mock location)
        /// </summary>
        public async Task SendMockLocationNowAsync()
        {
            if (MockLocation == null) return;
            try
            {
                _lastApiLogTime = DateTime.Now;
                await SendLocationToApiAsync(MockLocation);
                System.Diagnostics.Debug.WriteLine($"[LocationService] Mock location sent immediately: {MockLocation.Latitude}, {MockLocation.Longitude}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] SendMockLocationNow error: {ex.Message}");
            }
        }

        /// <summary>
        /// Gửi vị trí thật ngay lập tức (dùng khi tắt mock mode)
        /// </summary>
        public async Task SendRealLocationNowAsync()
        {
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5));
                var location = await Geolocation.Default.GetLocationAsync(request);
                if (location != null)
                {
                    _lastApiLogTime = DateTime.Now;
                    await SendLocationToApiAsync(location);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] SendRealLocationNow error: {ex.Message}");
            }
        }

        private async Task SendLocationToApiAsync(Location location)
        {
            try
            {
                var baseUrl = ApiService.BaseUrl;
                if (string.IsNullOrEmpty(baseUrl)) return;

                HttpMessageHandler handler;
#if ANDROID
                var androidHandler = new Xamarin.Android.Net.AndroidMessageHandler();
                androidHandler.ServerCertificateCustomValidationCallback =
                    (_, cert, _, errors) =>
                        cert?.Issuer == "CN=localhost" ||
                        errors == System.Net.Security.SslPolicyErrors.None;
                handler = androidHandler;
#else
                handler = new HttpClientHandler();
#endif
                using var client = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
                client.Timeout = TimeSpan.FromSeconds(5);

                var deviceId = string.IsNullOrEmpty(DeviceInfo.Name) ? $"emu_{DateTime.Now.Ticks}" : DeviceInfo.Name;
                var request = new
                {
                    DeviceId = deviceId,
                    Latitude = location.Latitude,
                    Longitude = location.Longitude,
                    Timestamp = DateTime.Now,
                    IsActive = true,
                    IsMock = IsMocking
                };

                var response = await client.PostAsJsonAsync("/api/userlocation", request);
                if (response.IsSuccessStatusCode)
                    System.Diagnostics.Debug.WriteLine($"[LocationService] Location sent to API: {location.Latitude}, {location.Longitude}");
                else
                    System.Diagnostics.Debug.WriteLine($"[LocationService] API FAILED: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] SendLocationToApiAsync error: {ex.Message}");
            }
        }
    }
}

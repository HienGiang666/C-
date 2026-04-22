using Microsoft.Maui.Devices.Sensors;

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
        public event EventHandler<Location>? LocationChanged;
        private bool _isTracking = false;
        private readonly object _trackingLock = new();
        private CancellationTokenSource? _trackingCts;

        public bool IsMocking { get; set; } = false;
        public Location? MockLocation { get; set; }
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

#if ANDROID
        private bool _useAndroidForegroundService = false;

        /// <summary>
        /// Enable foreground service mode (Android only)
        /// </summary>
        public void SetUseForegroundService(bool use)
        {
            _useAndroidForegroundService = use;
        }

        /// <summary>
        /// Start tracking with Android foreground service support
        /// </summary>
        public async Task StartTrackingWithForegroundAsync()
        {
            if (_useAndroidForegroundService)
            {
                // Request notification permission for foreground service (Android 13+)
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Tiramisu)
                {
                    var notifStatus = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
                    if (notifStatus != PermissionStatus.Granted)
                    {
                        notifStatus = await Permissions.RequestAsync<Permissions.PostNotifications>();
                    }
                }

                // Start foreground service
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context != null)
                {
                    TourApp.Mobile.Platforms.Android.Services.LocationForegroundService.Start(context);
                    return;
                }
            }

            // Fall back to standard tracking
            await StartTracking();
        }

        /// <summary>
        /// Stop tracking with foreground service cleanup (Android only)
        /// </summary>
        public void StopTrackingWithForeground()
        {
            if (_useAndroidForegroundService)
            {
                var context = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (context != null)
                {
                    TourApp.Mobile.Platforms.Android.Services.LocationForegroundService.Stop(context);
                }
            }
            StopTracking();
        }
#endif
    }
}

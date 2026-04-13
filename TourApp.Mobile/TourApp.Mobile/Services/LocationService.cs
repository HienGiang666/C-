using Microsoft.Maui.Devices.Sensors;

namespace TourApp.Mobile.Services
{
    public class LocationService
    {
        public event EventHandler<Location>? LocationChanged;
        private bool _isTracking = false;

        public bool IsMocking { get; set; } = false;
        public Location? MockLocation { get; set; }

        public async Task StartTracking()
        {
            if (_isTracking) return;

            try
            {
                // Permissions.RequestAsync() PHẢI chạy trên Main Thread (Activity context)
                PermissionStatus status;
                if (MainThread.IsMainThread)
                {
                    var perm = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>().ConfigureAwait(true);
                    if (perm != PermissionStatus.Granted)
                        perm = await Permissions.RequestAsync<Permissions.LocationWhenInUse>().ConfigureAwait(true);
                    status = perm;
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
                    System.Diagnostics.Debug.WriteLine("[LocationService] GPS permission denied.");
                    return;
                }

                _isTracking = true;

                // [FIX] Dùng SafeFireAndForget thay vì _ = Task.Run(...)
                // _ = Task.Run(...) nếu throw exception trước while-loop → UnobservedTaskException → crash lặng lẽ
                RunGpsLoopSafe();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] StartTracking error: {ex}");
            }
        }

        /// <summary>
        /// Chạy GPS polling loop an toàn — mọi exception đều được bắt,
        /// không bao giờ làm crash app.
        /// </summary>
        private void RunGpsLoopSafe()
        {
            Task.Run(async () =>
            {
                while (_isTracking)
                {
                    try
                    {
                        if (IsMocking && MockLocation != null)
                        {
                            MainThread.BeginInvokeOnMainThread(() =>
                            {
                                try { LocationChanged?.Invoke(this, MockLocation); }
                                catch (Exception uiEx) { System.Diagnostics.Debug.WriteLine($"[LocationService] UI callback error: {uiEx.Message}"); }
                            });
                        }
                        else
                        {
                            var request = new GeolocationRequest(GeolocationAccuracy.Low, TimeSpan.FromSeconds(5));
                            var location = await Geolocation.Default.GetLocationAsync(request).ConfigureAwait(false);

                            if (location != null)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    try { LocationChanged?.Invoke(this, location); }
                                    catch (Exception uiEx)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[LocationService] UI callback error: {uiEx.Message}");
                                    }
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LocationService] GPS poll error: {ex.Message}");
                        // Không re-throw — loop tiếp tục chạy
                    }

                    // Tăng chu kỳ polling lên 6s để tiết kiệm pin/CPU
                    await Task.Delay(6000).ConfigureAwait(false);
                }
            }).ContinueWith(t =>
            {
                // Bắt mọi exception không được catch bên trong Task.Run
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"[LocationService] GPS loop faulted: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public void StopTracking()
        {
            _isTracking = false;
        }
    }
}

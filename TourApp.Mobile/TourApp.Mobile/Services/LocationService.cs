using Microsoft.Maui.Devices.Sensors;

namespace TourApp.Mobile.Services
{
    public class LocationService
    {
        public event EventHandler<Location>? LocationChanged;
        private bool _isTracking = false;

        public async Task StartTracking()
        {
            if (_isTracking) return;

            try
            {
                // [FIX DEADLOCK] Permissions.RequestAsync() cần Main Thread.
                // Nếu đã ở Main Thread thì gọi trực tiếp (InvokeOnMainThreadAsync khi đã ở Main Thread → deadlock).
                PermissionStatus status;
                if (MainThread.IsMainThread)
                {
                    // Đang ở Main Thread → gọi trực tiếp, không wrap thêm
                    status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (status != PermissionStatus.Granted)
                        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                }
                else
                {
                    // Đang ở background thread → cần dispatch lên Main Thread
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
                System.Diagnostics.Debug.WriteLine("[LocationService] GPS tracking started.");

                // GPS polling loop — chạy ở background thread
                _ = Task.Run(async () =>
                {
                    while (_isTracking)
                    {
                        try
                        {
                            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(2));
                            var location = await Geolocation.Default.GetLocationAsync(request);

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
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocationService] GPS poll error: {ex.Message}");
                        }

                        await Task.Delay(5000); // Poll mỗi 5 giây
                    }
                    System.Diagnostics.Debug.WriteLine("[LocationService] GPS loop stopped.");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LocationService] StartTracking error: {ex}");
            }
        }

        public void StopTracking()
        {
            _isTracking = false;
        }
    }
}

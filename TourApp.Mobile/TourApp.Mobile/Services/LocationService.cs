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
                // [CRITICAL] Permissions.RequestAsync() BẮT BUỘC phải chạy trên Main Thread
                // Nếu gọi từ ThreadPool (Task.Run) → crash Android (không có Activity context)
                var status = await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var perm = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
                    if (perm != PermissionStatus.Granted)
                        perm = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                    return perm;
                });

                if (status != PermissionStatus.Granted)
                {
                    System.Diagnostics.Debug.WriteLine("[LocationService] GPS permission denied.");
                    return;
                }

                _isTracking = true;

                // GPS polling loop chạy trên background thread (đúng)
                _ = Task.Run(async () =>
                {
                    while (_isTracking)
                    {
                        try
                        {
                            // [FIX] Timeout 2s thay vì 5s — tránh block thread quá lâu gây ANR
                            // Medium accuracy: lock GPS nhanh hơn, đủ tốt cho Geofence 20-80m
                            var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(2));
                            var location = await Geolocation.Default.GetLocationAsync(request);

                            if (location != null)
                            {
                                MainThread.BeginInvokeOnMainThread(() =>
                                {
                                    LocationChanged?.Invoke(this, location);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LocationService] GPS error: {ex.Message}");
                        }

                        await Task.Delay(5000); // Poll mỗi 5 giây
                    }
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

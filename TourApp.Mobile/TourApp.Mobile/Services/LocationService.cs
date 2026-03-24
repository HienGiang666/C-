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
            
            var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (permission != PermissionStatus.Granted)
            {
                permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
                if (permission != PermissionStatus.Granted) return;
            }

            _isTracking = true;

            // Simple polling loop for MVP (Foreground).
            // For true background on Android, a Foreground Service (Native) is required.
            _ = Task.Run(async () =>
            {
                while (_isTracking)
                {
                    try
                    {
                        var request = new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(5));
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
                        // Handle exceptions (e.g. location disabled)
                        Console.WriteLine($"Location tracking error: {ex.Message}");
                    }

                    await Task.Delay(5000); // 5 seconds interval
                }
            });
        }

        public void StopTracking()
        {
            _isTracking = false;
        }
    }
}

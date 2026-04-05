using TourApp.Mobile.Models;
using Microsoft.Maui.Media;

namespace TourApp.Mobile.Services
{
    public class GeofenceService
    {
        private readonly ApiService _apiService;
        private List<POI>? _pois;
        private int _lastSpokenPoiId = -1;
        private DateTime _lastSpokenTime = DateTime.MinValue;

        public event EventHandler<POI>? PoiTriggered;

        public GeofenceService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task InitializeAsync()
        {
            if (_pois == null)
            {
                try
                {
                    _pois = await Task.Run(async () =>
                    {
                        try { return await _apiService.GetAllPOIsAsync(); }
                        catch { return new List<POI>(); }
                    });
                }
                catch
                {
                    _pois = new List<POI>();
                }
            }
        }

        /// <summary>
        /// Inject POI list đã load sẵn từ MapPage, tránh gọi API lần 2.
        /// </summary>
        public void SetPois(List<POI> pois)
        {
            _pois = pois;
        }

        public void CheckGeofences(Location userLocation)
        {
            if (_pois == null || !_pois.Any()) return;

            // [FIX] Sort theo Priority — POI Priority=1 được kiểm tra trước
            var sortedPois = _pois.Where(p => p.IsActive).OrderBy(p => p.Priority);

            foreach (var poi in sortedPois)
            {
                var distance = Location.CalculateDistance(
                    userLocation.Latitude, userLocation.Longitude,
                    poi.Latitude, poi.Longitude, DistanceUnits.Kilometers) * 1000;

                if (distance <= poi.Radius)
                {
                    // Cooldown 2 phút/POI
                    if (poi.PoiId == _lastSpokenPoiId && (DateTime.Now - _lastSpokenTime).TotalMinutes < 2)
                    {
                        continue;
                    }

                    TriggerNarration(poi);
                    break; // Chỉ trigger 1 POI/cycle
                }
            }
        }

        private void TriggerNarration(POI poi)
        {
            _lastSpokenPoiId = poi.PoiId;
            _lastSpokenTime = DateTime.Now;

            // Notify UI (hiển bottom sheet + highlight marker)
            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                // [FIX] Highlight đúng marker trên map
                HighlightRequested?.Invoke(this, poi.PoiId);
            });

            // Phát TTS
            _ = SpeakNarrationAsync(poi);

            // [NEW] Ghi log narration (async, không chặn)
            _ = _apiService.LogNarrationAsync(poi.PoiId, null, "geofence");
        }

        // [NEW] Event để MapPage gọi highlightPoi() JS
        public event EventHandler<int>? HighlightRequested;

        private async Task SpeakNarrationAsync(POI poi)
        {
            try
            {
                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var viLocale = locales.FirstOrDefault(l => l.Language.Contains("vi", StringComparison.OrdinalIgnoreCase));

                var options = new SpeechOptions
                {
                    Pitch = 1.0f,
                    Volume = 1.0f,
                    Locale = viLocale
                };

                await TextToSpeech.Default.SpeakAsync($"Chào mừng bạn đến {poi.PoiName}. {poi.Description}", options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Error with locale: {ex.Message}");
                try
                {
                    // Fallback: không dùng locale, không crash app nếu TTS không khả dụng
                    await TextToSpeech.Default.SpeakAsync($"Chào mừng bạn đến {poi.PoiName}. {poi.Description}");
                }
                catch (Exception ex2)
                {
                    // TTS hoàn toàn không khả dụng trên thiết bị này — bỏ qua, không crash
                    System.Diagnostics.Debug.WriteLine($"[TTS] Fallback also failed: {ex2.Message}");
                }
            }
        }
    }
}

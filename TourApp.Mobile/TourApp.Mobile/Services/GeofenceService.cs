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

        /// <summary>
        /// Ngôn ngữ hiện tại của app (có thể thay đổi từ UI settings).
        /// vi | en | zh | ja | ko | fr | es | de | th | ru
        /// </summary>
        public string CurrentLanguage { get; set; } = "vi";

        public event EventHandler<POI>? PoiTriggered;
        public event EventHandler<int>? HighlightRequested;

        // Map language code → TTS locale
        private static readonly Dictionary<string, string> LangToLocale = new()
        {
            ["vi"] = "vi-VN",
            ["en"] = "en-US",
            ["zh"] = "zh-CN",
            ["ja"] = "ja-JP",
            ["ko"] = "ko-KR",
            ["fr"] = "fr-FR",
            ["es"] = "es-ES",
            ["de"] = "de-DE",
            ["th"] = "th-TH",
            ["ru"] = "ru-RU",
        };

        public GeofenceService(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task InitializeAsync()
        {
            if (_pois != null) return;

            try
            {
                // Tránh Task.Run thừa gây switch thread â€“ thiết bị yếu dễ drop frame
                _pois = await _apiService.GetAllPOIsAsync();
            }
            catch
            {
                _pois = new List<POI>();
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

            // Ưu tiên POI gần nhất trước, sau đó tới priority để giảm CPU
            var sortedPois = _pois
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    Poi = p,
                    Distance = Location.CalculateDistance(
                        userLocation.Latitude, userLocation.Longitude,
                        p.Latitude, p.Longitude, DistanceUnits.Kilometers) * 1000
                })
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Poi.Priority);

            foreach (var poi in sortedPois)
            {
                if (poi.Distance <= poi.Poi.Radius)
                {
                    // Cooldown 2 phút/POI
                    if (poi.Poi.PoiId == _lastSpokenPoiId && (DateTime.Now - _lastSpokenTime).TotalMinutes < 2)
                        continue;

                    TriggerNarration(poi.Poi);
                    break; // Chỉ trigger 1 POI/cycle
                }
            }
        }

        private void TriggerNarration(POI poi)
        {
            _lastSpokenPoiId = poi.PoiId;
            _lastSpokenTime = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                HighlightRequested?.Invoke(this, poi.PoiId);
            });

            _ = SpeakNarrationAsync(poi);
            _ = _apiService.LogNarrationAsync(poi.PoiId, null, "geofence");
        }

        /// <summary>
        /// Phát TTS dùng ScriptText từ DB (đúng ngôn ngữ hiện tại).
        /// Thứ tự ưu tiên: ScriptText[CurrentLanguage] → ScriptText[vi] → Description fallback.
        /// </summary>
        public async Task SpeakNarrationAsync(POI poi, string? overrideLang = null)
        {
            try
            {
                var lang = overrideLang ?? CurrentLanguage;
                var script = poi.GetScript(lang);
                var localeName = LangToLocale.TryGetValue(lang, out var loc) ? loc : "vi-VN";

                System.Diagnostics.Debug.WriteLine($"[TTS] POI={poi.PoiName}, lang={lang}, script={script.Substring(0, Math.Min(50, script.Length))}...");

                var locales = await TextToSpeech.Default.GetLocalesAsync();
                var matchedLocale = locales.FirstOrDefault(l =>
                    l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));

                var options = new SpeechOptions
                {
                    Pitch = 1.0f,
                    Volume = 1.0f,
                    Locale = matchedLocale // null = system default, vẫn OK
                };

                await TextToSpeech.Default.SpeakAsync(script, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Error with locale: {ex.Message}");
                try
                {
                    // Fallback hoàn toàn không locale
                    var script = poi.GetScript("vi");
                    await TextToSpeech.Default.SpeakAsync(script);
                }
                catch (Exception ex2)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] Fallback failed: {ex2.Message}");
                }
            }
        }

        /// <summary>
        /// Phát audio từ QR Code — bypass GPS radius và cooldown.
        /// </summary>
        public async Task TriggerFromQRAsync(POI poi)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] Force trigger POI={poi.PoiName}");

            // Reset cooldown để QR luôn phát được
            _lastSpokenPoiId = -1;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                HighlightRequested?.Invoke(this, poi.PoiId);
            });

            await SpeakNarrationAsync(poi);
            _ = _apiService.LogNarrationAsync(poi.PoiId, null, "qr");
        }
    }
}

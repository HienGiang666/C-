using TourApp.Mobile.Models;
using Microsoft.Maui.Media;

namespace TourApp.Mobile.Services
{
    /// <summary>
    /// GeofenceService - Quản lý trigger địa lý và phát audio TTS
    /// 
    /// ĐỒNG BỘ NGÔN NGỮ:
    /// - LanguageService.CurrentLanguage được sync tự động vào GeofenceService.CurrentLanguage
    /// - Khi user đổi ngôn ngữ, LanguageService sẽ gọi SyncWithGeofenceService()
    /// 
    /// DỮ LIỆU AUDIO ĐA NGÔN NGỮ TỪ CMS/API:
    /// - CMS lưu Audio với: POIId, Language (vi/en/zh...), ScriptText, AudioPath
    /// - API trả về: POI.Audios[] chứa các bản audio cho từng ngôn ngữ
    /// - Mobile dùng: poi.GetScript(lang) để lấy ScriptText đúng ngôn ngữ
    /// - Fallback: lang đã chọn → vi → Description
    /// </summary>
    public class GeofenceService
    {
        private readonly ApiService _apiService;
        private List<POI>? _pois;
        private int _lastSpokenPoiId = -1;
        private DateTime _lastSpokenTime = DateTime.MinValue;
        private CancellationTokenSource? _ttsCts;

        /// <summary>
        /// Ngôn ngữ hiện tại của app (có thể thay đổi từ UI settings).
        /// vi | en | zh | ja
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
                    if (poi.Poi.Id == _lastSpokenPoiId && (DateTime.Now - _lastSpokenTime).TotalMinutes < 2)
                        continue;

                    TriggerNarration(poi.Poi);
                    break; // Chỉ trigger 1 POI/cycle
                }
            }
        }

        private void TriggerNarration(POI poi)
        {
            _lastSpokenPoiId = poi.Id;
            _lastSpokenTime = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                HighlightRequested?.Invoke(this, poi.Id);
            });

            _ = SpeakNarrationAsync(poi);
            _ = _apiService.LogNarrationAsync(poi.Id, null, "geofence");
        }

        /// <summary>
        /// Phát TTS dùng ScriptText từ DB (đúng ngôn ngữ hiện tại).
        /// Thứ tự ưu tiên: ScriptText[CurrentLanguage] → ScriptText[vi] → Description fallback.
        /// </summary>
        public async Task SpeakNarrationAsync(POI poi, string? overrideLang = null)
        {
            if (poi == null) return;

            try
            {
                // Dừng TTS (Text-to-Speech) nếu đang phát
                CancelTTS();
                _ttsCts = new CancellationTokenSource();
                var token = _ttsCts.Token;

                var lang = overrideLang ?? CurrentLanguage;

                // 1. Try to fetch MP3 Audio from API
                Audio? audio = null;
                try
                {
                    audio = await _apiService.GetAudioByPoiAsync(poi.Id, lang);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GeofenceService] Failed to get audio: {ex.Message}");
                }

                if (audio != null && !string.IsNullOrEmpty(audio.AudioPath))
                {
                    var audioUrl = audio.AudioPath.StartsWith("http")
                        ? audio.AudioPath
                        : ApiService.BaseUrl + audio.AudioPath;

                    try
                    {
                        await AudioPlayerService.Instance.PlayFromUrlAsync(audioUrl, poi.Name ?? "Audio");
                        // Log metrics
                        _ = _apiService.LogNarrationAsync(poi.Id, audio.Id, "geofence");
                        return; // Successfully played real audio
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GeofenceService] Failed to play audio: {ex.Message}");
                        // Continue to TTS fallback
                    }
                }

                // 2. Fallback to TTS if no MP3 found
                var script = poi.GetScript(lang);
                if (string.IsNullOrWhiteSpace(script)) return;

                var localeName = LangToLocale.TryGetValue(lang, out var loc) ? loc : "vi-VN";
                System.Diagnostics.Debug.WriteLine($"[TTS] POI={poi.Name}, lang={lang}, script={script[..Math.Min(50, script.Length)]}...");

                try
                {
                    var locales = await TextToSpeech.Default.GetLocalesAsync();
                    var matchedLocale = locales?.FirstOrDefault(l =>
                        l.Language.StartsWith(lang, StringComparison.OrdinalIgnoreCase));

                    var options = new SpeechOptions
                    {
                        Pitch = 1.0f,
                        Volume = 1.0f,
                        Locale = matchedLocale
                    };

                    await TextToSpeech.Default.SpeakAsync(script, options, cancelToken: token);
                    _ = _apiService.LogNarrationAsync(poi.Id, null, "geofence_tts");
                }
                catch (FeatureNotSupportedException)
                {
                    System.Diagnostics.Debug.WriteLine("[TTS] Not supported on this device");
                }
                catch (PermissionException)
                {
                    System.Diagnostics.Debug.WriteLine("[TTS] Permission denied");
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Hủy TTS đang phát
        /// </summary>
        public void CancelTTS()
        {
            try
            {
                _ttsCts?.Cancel();
                _ttsCts?.Dispose();
                _ttsCts = null;
                System.Diagnostics.Debug.WriteLine("[TTS] Cancelled from CancelTTS()");
            }
            catch { }
        }

        /// <summary>
        /// Phát audio từ QR Code — bypass GPS radius và cooldown.
        /// </summary>
        public async Task TriggerFromQRAsync(POI poi)
        {
            System.Diagnostics.Debug.WriteLine($"[QR] Force trigger POI={poi.Name}");

            // Reset cooldown để QR luôn phát được
            _lastSpokenPoiId = -1;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                HighlightRequested?.Invoke(this, poi.Id);
            });

            await SpeakNarrationAsync(poi);
            _ = _apiService.LogNarrationAsync(poi.Id, null, "qr");
        }
    }
}

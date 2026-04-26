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
        private readonly Dictionary<int, DateTime> _poiCooldowns = new();
        private const double CooldownMinutes = 2;
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

            var now = DateTime.Now;

            // Tìm tất cả POI trong phạm vi, sắp xếp theo khoảng cách + priority
            var poisInRange = _pois
                .Where(p => p.IsActive)
                .Select(p => new
                {
                    Poi = p,
                    Distance = Location.CalculateDistance(
                        userLocation.Latitude, userLocation.Longitude,
                        p.Latitude, p.Longitude, DistanceUnits.Kilometers) * 1000
                })
                .Where(x => x.Distance <= x.Poi.Radius)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Poi.Priority)
                .ToList();

            foreach (var entry in poisInRange)
            {
                // Per-POI cooldown
                if (_poiCooldowns.TryGetValue(entry.Poi.Id, out var lastTime)
                    && (now - lastTime).TotalMinutes < CooldownMinutes)
                    continue;

                TriggerNarration(entry.Poi);
            }

            // Dọn cooldown cũ (> 10 phút) để tránh memory leak
            var expired = _poiCooldowns.Where(kv => (now - kv.Value).TotalMinutes > 10).Select(kv => kv.Key).ToList();
            foreach (var id in expired) _poiCooldowns.Remove(id);
        }

        private void TriggerNarration(POI poi)
        {
            _poiCooldowns[poi.Id] = DateTime.Now;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                PoiTriggered?.Invoke(this, poi);
                HighlightRequested?.Invoke(this, poi.Id);
            });

            _ = EnqueueAudioForPoiAsync(poi);
            _ = _apiService.LogNarrationAsync(poi.Id, null, "geofence");
        }

        /// <summary>
        /// Enqueue audio/TTS cho POI vào AudioPlayerService queue (không đè nhau)
        /// </summary>
        private async Task EnqueueAudioForPoiAsync(POI poi)
        {
            try
            {
                var lang = CurrentLanguage;

                // 1. Try MP3 audio từ API
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

                    await AudioPlayerService.Instance.EnqueueAsync(new AudioQueueItem
                    {
                        Url = audioUrl,
                        Title = poi.Name ?? "Audio",
                        PoiId = poi.Id
                    });
                    return;
                }

                // 2. Fallback: TTS (phát trực tiếp, không qua queue)
                await SpeakTTSAsync(poi, lang);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GeofenceService] EnqueueAudio error: {ex.Message}");
            }
        }

        /// <summary>
        /// Phát narration cho POI (gọi từ QR hoặc manual).
        /// Sử dụng queue nếu có MP3, fallback TTS nếu không.
        /// </summary>
        public async Task SpeakNarrationAsync(POI poi, string? overrideLang = null)
        {
            if (poi == null) return;

            var lang = overrideLang ?? CurrentLanguage;

            // 1. Try MP3 audio
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

                await AudioPlayerService.Instance.PlayFromUrlAsync(audioUrl, poi.Name ?? "Audio", poi.Id);
                _ = _apiService.LogNarrationAsync(poi.Id, audio.Id, "narration");
                return;
            }

            // 2. Fallback TTS
            await SpeakTTSAsync(poi, lang);
        }

        /// <summary>
        /// Fallback: phát TTS khi không có file MP3
        /// </summary>
        private async Task SpeakTTSAsync(POI poi, string lang)
        {
            try
            {
                CancelTTS();
                _ttsCts = new CancellationTokenSource();
                var token = _ttsCts.Token;

                var script = poi.GetScript(lang);
                if (string.IsNullOrWhiteSpace(script))
                    script = $"{poi.Name}. {poi.Description}";
                if (string.IsNullOrWhiteSpace(script)) return;

                System.Diagnostics.Debug.WriteLine($"[TTS] POI={poi.Name}, lang={lang}, script={script[..Math.Min(50, script.Length)]}...");

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
                _ = _apiService.LogNarrationAsync(poi.Id, null, "tts");
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
            _poiCooldowns.Remove(poi.Id);

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

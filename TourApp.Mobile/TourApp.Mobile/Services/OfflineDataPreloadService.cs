using System.Diagnostics;

namespace TourApp.Mobile.Services
{
    /// <summary>
    /// Service warm-up cache khi app khởi động có mạng.
    /// Tải toàn bộ POI, Tour, ảnh, map assets, language về local
    /// để khi offline app vẫn hoạt động đầy đủ.
    /// Chạy background (fire-and-forget) — không block UI thread.
    /// </summary>
    public static class OfflineDataPreloadService
    {
        // Map asset URLs (trùng với MapPage)
        private const string GoongJsUrl = "https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.js";
        private const string GoongCssUrl = "https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.css";
        private const string GoongStyleUrl = "https://tiles.goong.io/assets/goong_map_web.json";

        private static readonly string MapCacheDir = Path.Combine(FileSystem.AppDataDirectory, "map_cache");
        private static string JsCachePath => Path.Combine(MapCacheDir, "goong-js.js");
        private static string CssCachePath => Path.Combine(MapCacheDir, "goong-js.css");
        private static string StyleCachePath => Path.Combine(MapCacheDir, "style.json");

        private static bool _isRunning = false;

        /// <summary>
        /// Bắt đầu preload toàn bộ dữ liệu offline. Gọi 1 lần khi app khởi động có mạng.
        /// </summary>
        public static void StartPreload()
        {
            if (!NetworkService.IsConnected) return;
            if (_isRunning) return;

            _isRunning = true;
            _ = Task.Run(async () =>
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    Debug.WriteLine("[OfflinePreload] ===== START =====");

                    // 1. API data: POIs + Tours (đã tự cache vào file JSON)
                    var api = new ApiService();
                    var poisTask = api.GetAllPOIsAsync();
                    var toursTask = api.GetAllToursAsync();
                    var langsTask = api.GetLanguagesAsync();
                    var transTask = api.GetUiTranslationsAsync();

                    var pois = await poisTask;
                    var tours = await toursTask;
                    await langsTask;
                    await transTask;
                    Debug.WriteLine($"[OfflinePreload] API data done — POIs:{pois.Count} Tours:{tours.Count} ({sw.ElapsedMilliseconds}ms)");

                    // 2. Images: tất cả ảnh POI + Tour
                    var imageUrls = new List<string?>();
                    imageUrls.AddRange(pois.Select(p => p.ImageUrl));
                    imageUrls.AddRange(tours.Select(t => t.ImageUrl));

                    // Filter active only + distinct
                    var distinctUrls = imageUrls
                        .Where(u => !string.IsNullOrWhiteSpace(u))
                        .Distinct()
                        .ToList();

                    if (distinctUrls.Any())
                    {
                        await ImageCacheService.PreloadAsync(distinctUrls);
                        Debug.WriteLine($"[OfflinePreload] Images done — {distinctUrls.Count} images ({sw.ElapsedMilliseconds}ms)");
                    }

                    // 3. Map assets (JS/CSS/Style JSON)
                    await CacheMapAssetsAsync();
                    Debug.WriteLine($"[OfflinePreload] Map assets done ({sw.ElapsedMilliseconds}ms)");

                    Debug.WriteLine($"[OfflinePreload] ===== COMPLETE in {sw.ElapsedMilliseconds}ms =====");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[OfflinePreload] ERROR: {ex.Message}");
                }
                finally
                {
                    _isRunning = false;
                }
            });
        }

        private static async Task CacheMapAssetsAsync()
        {
            try
            {
                if (!NetworkService.IsConnected) return;
                if (File.Exists(JsCachePath) && File.Exists(CssCachePath) && File.Exists(StyleCachePath))
                    return;

                Directory.CreateDirectory(MapCacheDir);
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

                if (!File.Exists(JsCachePath))
                {
                    var js = await client.GetStringAsync(GoongJsUrl);
                    await File.WriteAllTextAsync(JsCachePath, js);
                    Debug.WriteLine($"[OfflinePreload] JS cached ({js.Length} chars)");
                }
                if (!File.Exists(CssCachePath))
                {
                    var css = await client.GetStringAsync(GoongCssUrl);
                    await File.WriteAllTextAsync(CssCachePath, css);
                    Debug.WriteLine($"[OfflinePreload] CSS cached ({css.Length} chars)");
                }
                if (!File.Exists(StyleCachePath))
                {
                    var style = await client.GetStringAsync(GoongStyleUrl);
                    await File.WriteAllTextAsync(StyleCachePath, style);
                    Debug.WriteLine($"[OfflinePreload] Style JSON cached ({style.Length} chars)");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OfflinePreload] Map asset error: {ex.Message}");
            }
        }
    }
}

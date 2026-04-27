using System.Collections.Concurrent;
using System.Diagnostics;

namespace TourApp.Mobile.Services
{
    public static class ImageCacheService
    {
        private static readonly ConcurrentDictionary<string, string> _cache = new();
        private static readonly string _cacheDir = Path.Combine(FileSystem.AppDataDirectory, "img_cache");
        private static HttpClient? _client;

        private static HttpClient Client
        {
            get
            {
                if (_client != null) return _client;

                HttpMessageHandler handler;
#if ANDROID
                handler = new Xamarin.Android.Net.AndroidMessageHandler();
#else
                handler = new HttpClientHandler();
#endif
                _client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(15) };
                return _client;
            }
        }

        static ImageCacheService()
        {
            try { Directory.CreateDirectory(_cacheDir); } catch { }
        }

        public static string ResolveUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return "";

            var baseUrl = ApiService.BaseUrl.TrimEnd('/');

            // CMS lưu URL dạng "https://localhost:7031/uploads/..."
            // Phone không truy cập được localhost → thay bằng API BaseUrl
            if (url.Contains("localhost") || url.Contains("127.0.0.1"))
            {
                try
                {
                    var uri = new Uri(url);
                    var pathAndQuery = uri.PathAndQuery; // "/uploads/images/xxx.jpg"
                    Debug.WriteLine($"[ImageCache] Rewrite localhost URL: {url} → {baseUrl}{pathAndQuery}");
                    return baseUrl + pathAndQuery;
                }
                catch { /* fall through */ }
            }

            if (url.StartsWith("http://") || url.StartsWith("https://"))
                return url;

            return baseUrl + (url.StartsWith("/") ? url : "/" + url);
        }

        /// <summary>
        /// Download ảnh từ URL, cache vào file local, trả về đường dẫn file.
        /// Nếu đã cache rồi thì trả về ngay.
        /// </summary>
        public static async Task<string?> GetLocalPathAsync(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return null;

            var fullUrl = ResolveUrl(imageUrl);
            if (string.IsNullOrEmpty(fullUrl)) return null;

            // Check in-memory cache
            if (_cache.TryGetValue(fullUrl, out var cached) && File.Exists(cached))
                return cached;

            // App restart: dictionary empty nhưng file vẫn còn trên disk
            var hash = fullUrl.GetHashCode().ToString("X8");
            var diskExt = GetExtension(fullUrl);
            var diskPath = Path.Combine(_cacheDir, $"{hash}{diskExt}");
            if (File.Exists(diskPath))
            {
                _cache[fullUrl] = diskPath;
                Debug.WriteLine($"[ImageCache] Disk hit (restart): {diskPath}");
                return diskPath;
            }

            try
            {
                Debug.WriteLine($"[ImageCache] Downloading: {fullUrl}");
                var response = await Client.GetAsync(fullUrl);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[ImageCache] HTTP {response.StatusCode}: {fullUrl}");
                    return null;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0) return null;

                await File.WriteAllBytesAsync(diskPath, bytes);
                _cache[fullUrl] = diskPath;

                Debug.WriteLine($"[ImageCache] OK {bytes.Length}B → {diskPath}");
                return diskPath;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ImageCache] Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Pre-download nhiều ảnh song song.
        /// </summary>
        public static async Task PreloadAsync(IEnumerable<string?> urls)
        {
            var tasks = urls
                .Where(u => !string.IsNullOrWhiteSpace(u))
                .Distinct()
                .Select(u => GetLocalPathAsync(u));
            await Task.WhenAll(tasks);
        }

        private static string GetExtension(string url)
        {
            try
            {
                var ext = Path.GetExtension(new Uri(url).AbsolutePath);
                return string.IsNullOrEmpty(ext) ? ".jpg" : ext;
            }
            catch { return ".jpg"; }
        }
    }
}

using System.Globalization;
using System.Diagnostics;

namespace TourApp.Mobile.Converters
{
    public class UrlToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string url && !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    // Nếu là absolute URL, dùng trực tiếp
                    if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                    {
                        Debug.WriteLine($"[ImageConverter] Loading absolute URL: {url}");
                        return ImageSource.FromUri(absoluteUri);
                    }

                    // Nếu là relative URL, nối với BaseUrl
                    if (url.StartsWith("/"))
                    {
                        var baseUrl = Services.ApiService.BaseUrl;
                        var fullUrl = baseUrl.TrimEnd('/') + url;
                        Debug.WriteLine($"[ImageConverter] Loading relative URL: {fullUrl}");
                        return ImageSource.FromUri(new Uri(fullUrl));
                    }

                    // Các trường hợp khác, thử parse
                    Debug.WriteLine($"[ImageConverter] Loading URL: {url}");
                    return ImageSource.FromUri(new Uri(url));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageConverter] Error loading {url}: {ex.Message}");
                    return ImageSource.FromFile("dotnet_bot.png");
                }
            }
            Debug.WriteLine("[ImageConverter] Empty URL, using placeholder");
            return ImageSource.FromFile("dotnet_bot.png");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

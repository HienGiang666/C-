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
                    // Trim whitespace
                    url = url.Trim();
                    
                    // Nếu là absolute URL (http:// hoặc https://), dùng trực tiếp
                    if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
                        url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.WriteLine($"[ImageConverter] Loading absolute URL: {url}");
                        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
                        {
                            return ImageSource.FromUri(absoluteUri);
                        }
                    }
                    // Nếu là relative URL bắt đầu bằng /, nối với BaseUrl
                    else if (url.StartsWith("/"))
                    {
                        var baseUrl = Services.ApiService.BaseUrl;
                        var fullUrl = baseUrl.TrimEnd('/') + url;
                        Debug.WriteLine($"[ImageConverter] Loading relative URL: {fullUrl}");
                        if (Uri.TryCreate(fullUrl, UriKind.Absolute, out var fullUri))
                        {
                            return ImageSource.FromUri(fullUri);
                        }
                    }
                    // URL không có scheme, thêm http://
                    else
                    {
                        var fullUrl = $"http://{url}";
                        Debug.WriteLine($"[ImageConverter] Adding scheme to URL: {fullUrl}");
                        if (Uri.TryCreate(fullUrl, UriKind.Absolute, out var schemeUri))
                        {
                            return ImageSource.FromUri(schemeUri);
                        }
                    }
                    
                    Debug.WriteLine($"[ImageConverter] Failed to parse URL: {url}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImageConverter] Error loading '{url}': {ex.Message}");
                }
            }
            
            Debug.WriteLine($"[ImageConverter] Empty or invalid URL '{value}', using placeholder");
            return ImageSource.FromFile("dotnet_bot.png");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System.Globalization;
using System.Diagnostics;

namespace TourApp.Mobile.Converters
{
    /// <summary>
    /// Converter đơn giản: nhận local file path (đã download bởi ImageCacheService),
    /// trả về ImageSource.FromFile. Nếu là URL chưa cache → trả null.
    /// </summary>
    public class UrlToImageSourceConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string path || string.IsNullOrWhiteSpace(path))
                return null;

            // Nếu là local file path (đã cache)
            if (File.Exists(path))
            {
                Debug.WriteLine($"[ImageConverter] FromFile: {path}");
                return ImageSource.FromFile(path);
            }

            // Nếu vẫn là URL (chưa được pre-download), log warning
            Debug.WriteLine($"[ImageConverter] Not cached yet: {path}");
            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

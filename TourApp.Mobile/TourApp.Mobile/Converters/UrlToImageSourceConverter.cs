using System.Globalization;

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
                    return ImageSource.FromUri(new Uri(url));
                }
                catch
                {
                    return ImageSource.FromFile("dotnet_bot.png");
                }
            }
            return ImageSource.FromFile("dotnet_bot.png");
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

using System.Globalization;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Converters;

/// <summary>
/// Converter: format value với localized string key.
/// Usage: Binding PoiCount, Converter={StaticResource LocalizedFormat}, ConverterParameter=PoiCount
/// → LanguageService.GetString("PoiCount", value) → "3 điểm" / "3 stops" / ...
/// </summary>
public class LocalizedFormatConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is string key && value != null)
        {
            return LanguageService.GetString(key, value);
        }
        return value?.ToString() ?? "";
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

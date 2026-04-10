namespace TourApp.CMS.Models;

public class SettingsViewModel
{
    public List<LanguageOption> Languages { get; set; } = new();
    public List<LanguageOption> AvailableLanguages { get; set; } = new();
    public string NewLanguageCode { get; set; } = string.Empty;
    public string NewLanguageName { get; set; } = string.Empty;
    public string NewLanguageLocale { get; set; } = string.Empty;
}

using TourApp.CMS.Models;

namespace TourApp.CMS.Services;

public interface ILanguageSettingsService
{
    Task<List<LanguageOption>> GetAllAsync();
    List<LanguageOption> GetLanguageCatalog();
    Task<bool> AddCustomLanguageAsync(LanguageOption language);
    Task<bool> RemoveCustomLanguageAsync(string code);
    bool IsDefaultLanguage(string code);
}

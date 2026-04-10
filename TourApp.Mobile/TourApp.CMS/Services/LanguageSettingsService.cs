using System.Text.Json;
using TourApp.CMS.Models;

namespace TourApp.CMS.Services;

public class LanguageSettingsService : ILanguageSettingsService
{
    private static readonly List<LanguageOption> DefaultLanguages =
    [
        new() { Code = "vi", Name = "Tiếng Việt", Locale = "vi-VN" },
        new() { Code = "en", Name = "Tiếng Anh", Locale = "en-US" },
        new() { Code = "zh", Name = "Tiếng Trung", Locale = "zh-CN" },
        new() { Code = "ja", Name = "Tiếng Nhật", Locale = "ja-JP" }
    ];

    private readonly string _storagePath;
    private static readonly List<LanguageOption> Catalog =
    [
        new() { Code = "vi", Name = "Tiếng Việt", Locale = "vi-VN" },
        new() { Code = "en", Name = "Tiếng Anh", Locale = "en-US" },
        new() { Code = "zh", Name = "Tiếng Trung", Locale = "zh-CN" },
        new() { Code = "ja", Name = "Tiếng Nhật", Locale = "ja-JP" },
        new() { Code = "ko", Name = "Tiếng Hàn", Locale = "ko-KR" },
        new() { Code = "fr", Name = "Tiếng Pháp", Locale = "fr-FR" },
        new() { Code = "de", Name = "Tiếng Đức", Locale = "de-DE" },
        new() { Code = "es", Name = "Tiếng Tây Ban Nha", Locale = "es-ES" },
        new() { Code = "it", Name = "Tiếng Ý", Locale = "it-IT" },
        new() { Code = "pt", Name = "Tiếng Bồ Đào Nha", Locale = "pt-PT" },
        new() { Code = "ru", Name = "Tiếng Nga", Locale = "ru-RU" },
        new() { Code = "th", Name = "Tiếng Thái", Locale = "th-TH" },
        new() { Code = "id", Name = "Tiếng Indonesia", Locale = "id-ID" },
        new() { Code = "ms", Name = "Tiếng Malaysia", Locale = "ms-MY" },
        new() { Code = "ar", Name = "Tiếng Ả Rập", Locale = "ar-SA" },
        new() { Code = "hi", Name = "Tiếng Hindi", Locale = "hi-IN" },
        new() { Code = "tr", Name = "Tiếng Thổ Nhĩ Kỳ", Locale = "tr-TR" },
        new() { Code = "nl", Name = "Tiếng Hà Lan", Locale = "nl-NL" },
        new() { Code = "pl", Name = "Tiếng Ba Lan", Locale = "pl-PL" },
        new() { Code = "sv", Name = "Tiếng Thụy Điển", Locale = "sv-SE" }
    ];

    public LanguageSettingsService(IWebHostEnvironment env)
    {
        var folder = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(folder);
        _storagePath = Path.Combine(folder, "languages.json");
    }

    public bool IsDefaultLanguage(string code)
    {
        return DefaultLanguages.Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));
    }

    public List<LanguageOption> GetLanguageCatalog()
    {
        return Catalog
            .OrderBy(l => l.Code == "vi" ? 0 : 1)
            .ThenBy(l => l.Name)
            .ToList();
    }

    public async Task<List<LanguageOption>> GetAllAsync()
    {
        var customLanguages = await ReadCustomAsync();
        return DefaultLanguages
            .Concat(customLanguages)
            .GroupBy(l => l.Code.ToLowerInvariant())
            .Select(g => g.First())
            .OrderBy(l => l.Code == "vi" ? 0 : 1)
            .ThenBy(l => l.Code == "en" ? 0 : 1)
            .ThenBy(l => l.Code == "zh" ? 0 : 1)
            .ThenBy(l => l.Code == "ja" ? 0 : 1)
            .ThenBy(l => l.Name)
            .ToList();
    }

    public async Task<bool> AddCustomLanguageAsync(LanguageOption language)
    {
        if (string.IsNullOrWhiteSpace(language.Code) || string.IsNullOrWhiteSpace(language.Name))
        {
            return false;
        }

        language.Code = language.Code.Trim().ToLowerInvariant();
        language.Name = language.Name.Trim();
        if (string.IsNullOrWhiteSpace(language.Locale))
        {
            var inCatalog = Catalog.FirstOrDefault(x => x.Code.Equals(language.Code, StringComparison.OrdinalIgnoreCase));
            language.Locale = inCatalog?.Locale ?? $"{language.Code}-{language.Code.ToUpperInvariant()}";
            language.Name = string.IsNullOrWhiteSpace(language.Name) ? inCatalog?.Name ?? language.Code : language.Name;
        }
        else
        {
            language.Locale = language.Locale.Trim();
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(language.Code, "^[a-z]{2,10}$"))
        {
            return false;
        }

        if (IsDefaultLanguage(language.Code))
        {
            return false;
        }

        var customLanguages = await ReadCustomAsync();
        if (customLanguages.Any(l => l.Code.Equals(language.Code, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        customLanguages.Add(language);
        await WriteCustomAsync(customLanguages);
        return true;
    }

    public async Task<bool> RemoveCustomLanguageAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        code = code.Trim().ToLowerInvariant();
        if (IsDefaultLanguage(code))
        {
            return false;
        }

        var customLanguages = await ReadCustomAsync();
        var removed = customLanguages.RemoveAll(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            await WriteCustomAsync(customLanguages);
        }

        return removed;
    }

    private async Task<List<LanguageOption>> ReadCustomAsync()
    {
        if (!File.Exists(_storagePath))
        {
            return new List<LanguageOption>();
        }

        try
        {
            await using var stream = File.OpenRead(_storagePath);
            var data = await JsonSerializer.DeserializeAsync<List<LanguageOption>>(stream);
            return data ?? new List<LanguageOption>();
        }
        catch
        {
            return new List<LanguageOption>();
        }
    }

    private async Task WriteCustomAsync(List<LanguageOption> customLanguages)
    {
        await using var stream = File.Create(_storagePath);
        await JsonSerializer.SerializeAsync(stream, customLanguages, new JsonSerializerOptions { WriteIndented = true });
    }
}

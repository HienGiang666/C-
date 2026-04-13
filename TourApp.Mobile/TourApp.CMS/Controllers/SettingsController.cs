using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class SettingsController : Controller
{
    private readonly ILanguageSettingsService _languageSettingsService;
    private readonly IHttpClientFactory _clientFactory;

    public SettingsController(ILanguageSettingsService languageSettingsService, IHttpClientFactory clientFactory)
    {
        _languageSettingsService = languageSettingsService;
        _clientFactory = clientFactory;
    }

    public async Task<IActionResult> Index()
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        ViewData["Title"] = "Cài đặt";
        var vm = new SettingsViewModel
        {
            Languages = await _languageSettingsService.GetAllAsync(),
            AvailableLanguages = _languageSettingsService.GetLanguageCatalog()
        };
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> AddLanguage(SettingsViewModel vm)
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        var ok = await _languageSettingsService.AddCustomLanguageAsync(new LanguageOption
        {
            Code = vm.NewLanguageCode,
            Name = vm.NewLanguageName,
            Locale = vm.NewLanguageLocale
        });

        if (ok)
        {
            var all = await _languageSettingsService.GetAllAsync();
            var added = all.FirstOrDefault(x => x.Code.Equals(vm.NewLanguageCode, StringComparison.OrdinalIgnoreCase));
            if (added != null)
            {
                await CreateMissingAudioForLanguageAsync(added);
            }
        }

        TempData[ok ? "success" : "error"] = ok
            ? "Đã thêm ngôn ngữ mới và đồng bộ audio cho toàn bộ POI."
            : "Không thể thêm ngôn ngữ. Kiểm tra mã ngôn ngữ hoặc trùng lặp.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveLanguage(string code)
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        if (_languageSettingsService.IsDefaultLanguage(code))
        {
            TempData["error"] = "Không thể xóa ngôn ngữ mặc định.";
            return RedirectToAction(nameof(Index));
        }

        await DeleteAudioByLanguageAsync(code);
        var ok = await _languageSettingsService.RemoveCustomLanguageAsync(code);
        TempData[ok ? "success" : "error"] = ok
            ? "Đã xóa ngôn ngữ tùy chọn và toàn bộ audio ngôn ngữ này."
            : "Không thể xóa ngôn ngữ mặc định hoặc mã không hợp lệ.";
        return RedirectToAction(nameof(Index));
    }

    private async Task DeleteAudioByLanguageAsync(string code)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync("api/Audio");
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var audios = await response.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
        var toDelete = audios
            .Where(x => x.Language.Equals(code, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id)
            .ToList();

        foreach (var id in toDelete)
        {
            await client.DeleteAsync($"api/Audio/{id}");
        }
    }

    private async Task CreateMissingAudioForLanguageAsync(LanguageOption language)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync("api/Audio");
        if (!response.IsSuccessStatusCode)
        {
            return;
        }

        var allAudios = await response.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
        var groupedByPoi = allAudios.GroupBy(x => x.POIId);

        foreach (var poiGroup in groupedByPoi)
        {
            if (poiGroup.Any(x => x.Language.Equals(language.Code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var sourceText = poiGroup
                .FirstOrDefault(x => x.Language.Equals("vi", StringComparison.OrdinalIgnoreCase))?.ScriptText
                ?? poiGroup.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.ScriptText))?.ScriptText
                ?? string.Empty;

            if (string.IsNullOrWhiteSpace(sourceText))
            {
                continue;
            }

            var translated = await TranslateAsync(sourceText, language.Code);
            var item = new Audio
            {
                POIId = poiGroup.Key,
                Language = language.Code,
                ScriptText = translated,
                Duration = EstimateDuration(translated),
                IsActive = true,
                AudioPath = "TTS_ONLY"
            };
            await client.PostAsJsonAsync("api/Audio", item);
        }
    }

    private async Task<string> TranslateAsync(string sourceText, string targetCode)
    {
        try
        {
            using var http = new HttpClient();
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={targetCode}&dt=t&q={Uri.EscapeDataString(sourceText)}";
            var json = await http.GetStringAsync(url);
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement[0];
            var text = string.Join("", root.EnumerateArray().Select(x => x[0].GetString() ?? string.Empty));
            return string.IsNullOrWhiteSpace(text) ? sourceText : text;
        }
        catch
        {
            return sourceText;
        }
    }

    private static int EstimateDuration(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return 1;
        }

        var words = script.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Max(1, (int)Math.Ceiling(words / 2.5));
    }

    private bool IsAdmin()
    {
        return string.Equals(HttpContext.Session.GetString("Role"), "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

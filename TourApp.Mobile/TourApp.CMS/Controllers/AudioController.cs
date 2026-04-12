using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class AudioController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly ILanguageSettingsService _languageSettingsService;

    public AudioController(
        IHttpClientFactory clientFactory,
        IActivityLogger activityLogger,
        ILanguageSettingsService languageSettingsService)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
        _languageSettingsService = languageSettingsService;
    }

    public async Task<IActionResult> Index(int? poiId, int page = 1)
    {
        const int pageSize = 10;
        ViewData["Title"] = "Quản lý Thuyết minh (Audio)";
        var client = _clientFactory.CreateClient("TourApi");
        ViewBag.LanguageColumns = await _languageSettingsService.GetAllAsync();
        ViewBag.POIs = new Dictionary<int, string>();
        ViewBag.PoiCatalogMap = new Dictionary<int, int>();

        string url = "api/Audio";
        if (poiId.HasValue)
        {
            url += $"?poiId={poiId.Value}";
            ViewBag.CurrentPoiId = poiId.Value;
        }

        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var audios = await response.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
            var role = HttpContext.Session.GetString("Role") ?? "";
            var userIdStr = HttpContext.Session.GetString("UserId");
            string poiUrl = "api/POI";
            if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) && int.TryParse(userIdStr, out var oid))
                poiUrl += $"?ownerUserId={oid}";

            var poiResponse = await client.GetAsync(poiUrl);
            if (poiResponse.IsSuccessStatusCode)
            {
                var pois = await poiResponse.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
                ViewBag.POIs = pois.ToDictionary(p => p.Id, p => p.Name);
                ViewBag.PoiCatalogMap = pois.ToDictionary(
                    p => p.Id,
                    p => p.PublicCatalogNumber > 0 ? p.PublicCatalogNumber : p.Id);
                if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase))
                {
                    var allowed = pois.Select(p => p.Id).ToHashSet();
                    audios = audios.Where(a => allowed.Contains(a.POIId)).ToList();
                }
            }

            var catalogMap = ViewBag.PoiCatalogMap as Dictionary<int, int> ?? new Dictionary<int, int>();
            var groupedKeys = audios.GroupBy(a => a.POIId).Select(g => g.Key).OrderBy(pid =>
                catalogMap.TryGetValue(pid, out var n) ? n : pid).ToList();
            page = Math.Max(1, page);
            var totalGroups = groupedKeys.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalGroups / (double)pageSize));
            if (page > totalPages) page = totalPages;
            var allowedIds = groupedKeys.Skip((page - 1) * pageSize).Take(pageSize).ToHashSet();
            audios = audios.Where(a => allowedIds.Contains(a.POIId)).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalGroupCount = totalGroups;
            return View(audios);
        }
        ViewBag.CurrentPage = 1;
        ViewBag.TotalPages = 1;
        ViewBag.TotalGroupCount = 0;
        return View(new List<Audio>());
    }

    public async Task<IActionResult> EditByPoi(int poiId)
    {
        var client = _clientFactory.CreateClient("TourApi");
        
        // Lấy thông tin POI để hiển thị tên
        var poiResponse = await client.GetAsync($"api/POI/{poiId}");
        string poiName = $"Địa điểm #{poiId}";
        if (poiResponse.IsSuccessStatusCode)
        {
            var poi = await poiResponse.Content.ReadFromJsonAsync<POI>();
            if (!string.IsNullOrWhiteSpace(poi?.Name))
                poiName = poi.Name;
        }
        
        var response = await client.GetAsync($"api/Audio?poiId={poiId}");
        if (!response.IsSuccessStatusCode)
        {
            TempData["error"] = "Không tải được dữ liệu thuyết minh của địa điểm.";
            return RedirectToAction(nameof(Index));
        }

        var audios = await response.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
        var languages = await _languageSettingsService.GetAllAsync();
        var scripts = languages.ToDictionary(
            l => l.Code,
            l => audios.FirstOrDefault(x => x.Language.Equals(l.Code, StringComparison.OrdinalIgnoreCase))?.ScriptText ?? string.Empty);

        var model = new AudioBulkCreateViewModel
        {
            POIId = poiId,
            SourceText = scripts.TryGetValue("vi", out var viText) ? viText : string.Empty,
            Scripts = scripts
        };

        ViewBag.Languages = languages;
        ViewBag.POIName = poiName;
        ViewData["Title"] = "Sửa thuyết minh theo địa điểm";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> EditByPoi(AudioBulkCreateViewModel model)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var existingResponse = await client.GetAsync($"api/Audio?poiId={model.POIId}");
        var existing = existingResponse.IsSuccessStatusCode
            ? await existingResponse.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>()
            : new List<Audio>();

        var languageSet = await _languageSettingsService.GetAllAsync();
        var validCodes = languageSet.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var pair in (model.Scripts ?? new Dictionary<string, string>()).Where(x => validCodes.Contains(x.Key)))
        {
            var script = (pair.Value ?? string.Empty).Trim();
            var old = existing.FirstOrDefault(x => x.Language.Equals(pair.Key, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(script))
            {
                if (old != null)
                {
                    await client.DeleteAsync($"api/Audio/{old.Id}");
                }
                continue;
            }

            if (old == null)
            {
                var create = new Audio
                {
                    POIId = model.POIId,
                    Language = pair.Key.ToLowerInvariant(),
                    ScriptText = script,
                    Duration = EstimateDuration(script),
                    IsActive = true,
                    AudioPath = "TTS_ONLY"
                };
                await client.PostAsJsonAsync("api/Audio", create);
            }
            else
            {
                old.ScriptText = script;
                old.Duration = EstimateDuration(script);
                old.IsActive = true;
                await client.PutAsJsonAsync($"api/Audio/{old.Id}", old);
            }
        }

        TempData["success"] = "Đã cập nhật toàn bộ thuyết minh của POI.";
        return RedirectToAction(nameof(Index), new { poiId = model.POIId });
    }

    public async Task<IActionResult> Create(int? poiId)
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["error"] = "Chỉ chủ quán ăn được thêm thuyết minh mới.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Thêm Thuyết minh";
        var vm = new AudioBulkCreateViewModel { POIId = poiId ?? 0 };
        await PrepareAudioCreateViewAsync(vm);
        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> Create(AudioBulkCreateViewModel model)
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["error"] = "Chỉ chủ quán ăn được thêm thuyết minh mới.";
            return RedirectToAction(nameof(Index));
        }

        if (model.POIId <= 0)
        {
            ModelState.AddModelError(nameof(model.POIId), "Vui lòng chọn địa điểm.");
        }

        var scripts = (model.Scripts ?? new Dictionary<string, string>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => new Audio
            {
                POIId = model.POIId,
                Language = x.Key.Trim().ToLowerInvariant(),
                ScriptText = x.Value.Trim(),
                Duration = EstimateDuration(x.Value),
                IsActive = true,
                AudioPath = "TTS_ONLY"
            })
            .ToList();

        if (scripts.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Vui lòng nhập nội dung ở ít nhất 1 ngôn ngữ.");
        }

        if (ModelState.IsValid && scripts.Count > 0)
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/Audio/bulk", scripts);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Create", "Audio", null, $"Bulk audio for POI {model.POIId}, total {scripts.Count}");
                TempData["success"] = "Đã lưu thuyết minh theo các ngôn ngữ đã cấu hình.";
                return RedirectToAction(nameof(Index), new { poiId = model.POIId });
            }

            TempData["error"] = "Lỗi khi lưu dữ liệu thuyết minh!";
        }

        await PrepareAudioCreateViewAsync(model);
        return View(model);
    }

    /// <summary>Chỉ POI của chủ quán; JSON danh sách POI đã có audio (để confirm ghi đè).</summary>
    private async Task PrepareAudioCreateViewAsync(AudioBulkCreateViewModel model)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var role = HttpContext.Session.GetString("Role") ?? "";
        var userIdStr = HttpContext.Session.GetString("UserId");
        var poiUrl = "api/POI";
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) && int.TryParse(userIdStr, out var oid))
            poiUrl += $"?ownerUserId={oid}";

        var poiResponse = await client.GetAsync(poiUrl);
        var pois = poiResponse.IsSuccessStatusCode
            ? await poiResponse.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>()
            : new List<POI>();

        var allowedIds = pois.Select(p => p.Id).ToHashSet();
        var audioPoiIds = new HashSet<int>();
        var audioResp = await client.GetAsync("api/Audio");
        if (audioResp.IsSuccessStatusCode)
        {
            var audios = await audioResp.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
            foreach (var a in audios)
            {
                if (allowedIds.Contains(a.POIId))
                    audioPoiIds.Add(a.POIId);
            }
        }

        ViewBag.PoiList = new SelectList(pois, "Id", "Name", model.POIId > 0 ? model.POIId : null);
        ViewBag.Languages = await _languageSettingsService.GetAllAsync();
        ViewBag.PoiIdsWithAudioJson = JsonSerializer.Serialize(audioPoiIds.OrderBy(x => x).ToList());
    }
    
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Chỉnh sửa Thuyết minh";
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync($"api/Audio/{id}");
        
        if (response.IsSuccessStatusCode)
        {
            var audio = await response.Content.ReadFromJsonAsync<Audio>();
            
            // Load POIs
            var poiResponse = await client.GetAsync("api/POI");
            var pois = poiResponse.IsSuccessStatusCode ? await poiResponse.Content.ReadFromJsonAsync<List<POI>>() : new List<POI>();
            ViewBag.PoiList = new SelectList(pois, "Id", "Name", audio?.POIId);
            ViewBag.Languages = await _languageSettingsService.GetAllAsync();
            
            return View(audio);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Audio audio)
    {
        var client = _clientFactory.CreateClient("TourApi");

        // Keep existing AudioPath vì không còn upload MP3.
        var existingResponse = await client.GetAsync($"api/Audio/{id}");
        if (existingResponse.IsSuccessStatusCode)
        {
            var existingAudio = await existingResponse.Content.ReadFromJsonAsync<Audio>();
            if (existingAudio != null)
            {
                audio.AudioPath = existingAudio.AudioPath;
            }
        }
        audio.Duration = EstimateDuration(audio.ScriptText);
        audio.IsActive = true;

        if (ModelState.IsValid)
        {
            var response = await client.PutAsJsonAsync($"api/Audio/{id}", audio);
            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Update", "Audio", null, $"Audio {id}");
                TempData["success"] = "Cập nhật audio thành công!";
                return RedirectToAction(nameof(Index), new { poiId = audio.POIId });
            }
            TempData["error"] = "Lỗi cập nhật CSDL!";
        }

        // Reload POIs on fail
        var poiResponse = await client.GetAsync("api/POI");
        var pois = poiResponse.IsSuccessStatusCode ? await poiResponse.Content.ReadFromJsonAsync<List<POI>>() : new List<POI>();
        ViewBag.PoiList = new SelectList(pois, "Id", "Name", audio.POIId);
        ViewBag.Languages = await _languageSettingsService.GetAllAsync();
        
        return View(audio);
    }
    
    public async Task<IActionResult> Delete(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        
        var getResponse = await client.GetAsync($"api/Audio/{id}");
        int? poiId = null;
        if (getResponse.IsSuccessStatusCode)
        {
            var audio = await getResponse.Content.ReadFromJsonAsync<Audio>();
            poiId = audio?.POIId;
        }

        await client.DeleteAsync($"api/Audio/{id}");
        _activityLogger.LogActivity(HttpContext, "Delete", "Audio", $"Audio {id}", null);
        TempData["success"] = "Xóa audio thành công!";
        return RedirectToAction(nameof(Index), new { poiId = poiId });
    }

    public async Task<IActionResult> DeleteByPoi(int poiId)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync($"api/Audio?poiId={poiId}");
        if (!response.IsSuccessStatusCode)
        {
            TempData["error"] = "Không thể tải danh sách audio để xóa.";
            return RedirectToAction(nameof(Index));
        }

        var audios = await response.Content.ReadFromJsonAsync<List<Audio>>() ?? new List<Audio>();
        foreach (var audio in audios)
        {
            await client.DeleteAsync($"api/Audio/{audio.Id}");
        }

        _activityLogger.LogActivity(HttpContext, "Delete", "Audio", $"POI {poiId}", null);
        TempData["success"] = "Đã xóa toàn bộ thuyết minh của địa điểm.";
        return RedirectToAction(nameof(Index));
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
}

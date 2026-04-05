using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class AudioController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly IFileUploadService _fileUploadService;

    public AudioController(IHttpClientFactory clientFactory, IActivityLogger activityLogger, IFileUploadService fileUploadService)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
        _fileUploadService = fileUploadService;
    }

    public async Task<IActionResult> Index(int? poiId)
    {
        ViewData["Title"] = "Quản lý Thuyết minh (Audio)";
        var client = _clientFactory.CreateClient("TourApi");
        
        string url = "api/Audio";
        if (poiId.HasValue)
        {
            url += $"?poiId={poiId.Value}";
            ViewBag.CurrentPoiId = poiId.Value;
        }

        var response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var audios = await response.Content.ReadFromJsonAsync<List<Audio>>();
            
            // Lấy thêm danh sách POI để hiển thị tên POI cho đẹp thay vì Id
            var poiResponse = await client.GetAsync("api/POI");
            if (poiResponse.IsSuccessStatusCode)
            {
                var pois = await poiResponse.Content.ReadFromJsonAsync<List<POI>>();
                ViewBag.POIs = pois?.ToDictionary(p => p.Id, p => p.Name);
            }

            return View(audios);
        }
        return View(new List<Audio>());
    }

    public async Task<IActionResult> Create(int? poiId)
    {
        ViewData["Title"] = "Thêm Thuyết minh";
        var client = _clientFactory.CreateClient("TourApi");
        var poiResponse = await client.GetAsync("api/POI");
        
        var pois = new List<POI>();
        if (poiResponse.IsSuccessStatusCode)
        {
            pois = await poiResponse.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
        }
        
        ViewBag.PoiList = new SelectList(pois, "Id", "Name", poiId);
        return View(new Audio { POIId = poiId ?? 0 });
    }

    [HttpPost]
    public async Task<IActionResult> Create(Audio audio, IFormFile? uploadAudio)
    {
        // Chỉ yêu cầu file âm thanh nếu KHÔNG có nội dung ScriptText (Text-To-Speech)
        if ((uploadAudio == null || uploadAudio.Length == 0) && string.IsNullOrWhiteSpace(audio.ScriptText))
        {
            ModelState.AddModelError("uploadAudio", "Vui lòng chọn file âm thanh hoặc nhập nội dung TTS");
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (uploadAudio != null && uploadAudio.Length > 0)
                {
                    audio.AudioPath = await _fileUploadService.UploadAudioAsync(uploadAudio, "audios");
                }
                else
                {
                    audio.AudioPath = "TTS_ONLY";
                }

                var client = _clientFactory.CreateClient("TourApi");
                var response = await client.PostAsJsonAsync("api/Audio", audio);

                if (response.IsSuccessStatusCode)
                {
                    _activityLogger.LogActivity(HttpContext, "Create", "Audio", null, $"Audio for POI {audio.POIId} ({audio.Language})");
                    TempData["success"] = "Tải lên file âm thanh thành công!";
                    return RedirectToAction(nameof(Index), new { poiId = audio.POIId });
                }
                TempData["error"] = "Lỗi khi lưu vào cơ sở dữ liệu!";
            }
            catch (Exception ex)
            {
                TempData["error"] = ex.Message;
            }
        }

        // Reload POI List on failure
        var poiclient = _clientFactory.CreateClient("TourApi");
        var poiResponse = await poiclient.GetAsync("api/POI");
        var pois = poiResponse.IsSuccessStatusCode ? await poiResponse.Content.ReadFromJsonAsync<List<POI>>() : new List<POI>();
        ViewBag.PoiList = new SelectList(pois, "Id", "Name", audio.POIId);

        return View(audio);
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
            
            return View(audio);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Audio audio, IFormFile? uploadAudio)
    {
        var client = _clientFactory.CreateClient("TourApi");
        
        if (uploadAudio != null && uploadAudio.Length > 0)
        {
            try
            {
                audio.AudioPath = await _fileUploadService.UploadAudioAsync(uploadAudio, "audios");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("uploadAudio", ex.Message);
            }
        }
        else
        {
            // Keep existing audio path
            var existingResponse = await client.GetAsync($"api/Audio/{id}");
            if (existingResponse.IsSuccessStatusCode)
            {
                var existingAudio = await existingResponse.Content.ReadFromJsonAsync<Audio>();
                if (existingAudio != null) audio.AudioPath = existingAudio.AudioPath;
            }
        }

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
            
            // Xóa file vật lý
            if (audio != null && !string.IsNullOrEmpty(audio.AudioPath))
            {
                _fileUploadService.DeleteImage(audio.AudioPath);
            }
        }

        await client.DeleteAsync($"api/Audio/{id}");
        _activityLogger.LogActivity(HttpContext, "Delete", "Audio", $"Audio {id}", null);
        TempData["success"] = "Xóa audio thành công!";
        return RedirectToAction(nameof(Index), new { poiId = poiId });
    }
}

using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class POIController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly IFileUploadService _fileUploadService;

    public POIController(IHttpClientFactory clientFactory, IActivityLogger activityLogger, IFileUploadService fileUploadService)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
        _fileUploadService = fileUploadService;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý Địa điểm";
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync("api/POI");

        if (response.IsSuccessStatusCode)
        {
            var pois = await response.Content.ReadFromJsonAsync<List<POI>>();
            return View(pois);
        }
        return View(new List<POI>());
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm Địa điểm";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(POI poi, IFormFile? uploadImage)
    {
        if (uploadImage != null)
        {
            poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");
        }

        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.PostAsJsonAsync("api/POI", poi);

        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Create", "POI", null, poi.Name);
            TempData["success"] = "Thêm địa điểm thành công!";
            return RedirectToAction(nameof(Index));
        }
        TempData["error"] = "Lỗi khi thêm địa điểm!";
        return View(poi);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        // Get name before deleting for the log
        var getResponse = await client.GetAsync($"api/POI/{id}");
        string? name = null;
        if (getResponse.IsSuccessStatusCode)
        {
            var poi = await getResponse.Content.ReadFromJsonAsync<POI>();
            name = poi?.Name;
        }

        await client.DeleteAsync($"api/POI/{id}");
        _activityLogger.LogActivity(HttpContext, "Delete", "POI", name, null);
        TempData["success"] = "Xóa địa điểm thành công!";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Cập nhật Địa điểm";
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync($"api/POI/{id}");

        if (response.IsSuccessStatusCode)
        {
            var poi = await response.Content.ReadFromJsonAsync<POI>();
            return View(poi);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, POI poi, IFormFile? uploadImage)
    {
        var client = _clientFactory.CreateClient("TourApi");
        
        // Preserve old image url if new image is not uploaded
        if (uploadImage != null)
        {
            poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");
        }
        else
        {
            var existingResponse = await client.GetAsync($"api/POI/{id}");
            if (existingResponse.IsSuccessStatusCode)
            {
                var existingPoi = await existingResponse.Content.ReadFromJsonAsync<POI>();
                if (existingPoi != null) poi.ImageUrl = existingPoi.ImageUrl;
            }
        }

        var response = await client.PutAsJsonAsync($"api/POI/{id}", poi);

        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Update", "POI", null, poi.Name);
            TempData["success"] = "Cập nhật địa điểm thành công!";
            return RedirectToAction(nameof(Index));
        }
        TempData["error"] = "Lỗi khi cập nhật!";
        return View(poi);
    }
}
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
        var url = BuildPoiListUrl();
        var response = await client.GetAsync(url);

        if (response.IsSuccessStatusCode)
        {
            var pois = await response.Content.ReadFromJsonAsync<List<POI>>();
            return View(pois ?? new List<POI>());
        }
        return View(new List<POI>());
    }

    private string BuildPoiListUrl()
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        var userId = HttpContext.Session.GetString("UserId");
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) && int.TryParse(userId, out var oid))
            return $"api/POI?ownerUserId={oid}";
        return "api/POI";
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm Địa điểm";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(POI poi, IFormFile? uploadImage)
    {
        SanitizePoi(poi);
        if (uploadImage != null)
            poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");

        ApplyOwnershipForSave(poi, isNew: true);

        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.PostAsJsonAsync("api/POI", poi);

        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Create", "POI", null, poi.Name);
            TempData["success"] = HttpContext.Session.GetString("Role") == "RestaurantOwner"
                ? "Đã gửi yêu cầu thêm địa điểm. Chờ Admin phê duyệt."
                : "Thêm địa điểm thành công!";
            return RedirectToAction(nameof(Index));
        }
        TempData["error"] = "Lỗi khi thêm địa điểm!";
        return View(poi);
    }

    public async Task<IActionResult> Delete(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var getResponse = await client.GetAsync($"api/POI/{id}");
        if (!getResponse.IsSuccessStatusCode)
            return RedirectToAction(nameof(Index));

        var poi = await getResponse.Content.ReadFromJsonAsync<POI>();
        if (!CanModifyPoi(poi))
        {
            TempData["error"] = "Bạn không có quyền xóa địa điểm này.";
            return RedirectToAction(nameof(Index));
        }

        string? name = poi?.Name;
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

        if (!response.IsSuccessStatusCode)
            return RedirectToAction(nameof(Index));

        var poi = await response.Content.ReadFromJsonAsync<POI>();
        if (!CanModifyPoi(poi))
        {
            TempData["error"] = "Bạn không có quyền sửa địa điểm này.";
            return RedirectToAction(nameof(Index));
        }

        return View(poi);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, POI poi, IFormFile? uploadImage)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var existingResponse = await client.GetAsync($"api/POI/{id}");
        if (!existingResponse.IsSuccessStatusCode)
            return RedirectToAction(nameof(Index));
        var existing = await existingResponse.Content.ReadFromJsonAsync<POI>();
        if (!CanModifyPoi(existing))
        {
            TempData["error"] = "Bạn không có quyền sửa địa điểm này.";
            return RedirectToAction(nameof(Index));
        }

        if (uploadImage != null)
            poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");
        else if (existing != null)
            poi.ImageUrl = existing.ImageUrl;

        poi.Id = id;
        SanitizePoi(poi);
        ApplyOwnershipForSave(poi, isNew: false, existing);

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

    private bool CanModifyPoi(POI? poi)
    {
        if (poi == null) return false;
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!int.TryParse(HttpContext.Session.GetString("UserId"), out var uid))
            return false;
        return poi.OwnerUserId == uid;
    }

    private void ApplyOwnershipForSave(POI poi, bool isNew, POI? existing = null)
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            if (isNew)
            {
                poi.ApprovalStatus = "Approved";
                poi.OwnerUserId ??= null;
            }
            return;
        }

        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(HttpContext.Session.GetString("UserId"), out var uid))
        {
            poi.OwnerUserId = uid;
            if (isNew)
                poi.ApprovalStatus = "Pending";
            else
            {
                poi.ApprovalStatus = existing?.ApprovalStatus ?? "Pending";
                if (existing?.ApprovalStatus == "Approved")
                    poi.ApprovalStatus = "Pending";
            }
        }
    }

    private static void SanitizePoi(POI poi)
    {
        if (poi.Radius < 0) poi.Radius = 0;
        if (poi.Priority < 0) poi.Priority = 0;
        if (poi.Rating < 0) poi.Rating = 0;
    }
}

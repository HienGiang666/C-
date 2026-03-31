using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class POIController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IFileUploadService _fileUploadService;
    private readonly IExportService _exportService;
    private readonly IActivityLogger _activityLogger;

    public POIController(
        IHttpClientFactory clientFactory,
        IFileUploadService fileUploadService,
        IExportService exportService,
        IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _fileUploadService = fileUploadService;
        _exportService = exportService;
        _activityLogger = activityLogger;
    }

    // Hiển thị danh sách địa điểm
    public async Task<IActionResult> Index(string search = "")
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/POI");

            List<POI> pois = new List<POI>();
            if (response.IsSuccessStatusCode)
            {
                pois = await response.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
            }

            // Tìm kiếm
            if (!string.IsNullOrEmpty(search))
            {
                pois = pois.Where(p => p.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                       p.Description.Contains(search, StringComparison.OrdinalIgnoreCase))
                           .ToList();
                ViewBag.SearchTerm = search;
            }

            ViewData["Title"] = "Quản lý Địa điểm";
            return View(pois);
        }
        catch
        {
            ViewData["Title"] = "Quản lý Địa điểm";
            return View(new List<POI>());
        }
    }

    // Export to Excel
    public async Task<IActionResult> Export()
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/POI");

            if (response.IsSuccessStatusCode)
            {
                var pois = await response.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();

                var excelData = _exportService.ExportToExcel(pois, "Địa điểm");
                _activityLogger.LogActivity(HttpContext, "Export", "POI", null, $"{pois.Count} items");

                return File(excelData, "application/vnd.ms-excel", $"Dia_Diem_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
            }

            TempData["error"] = "Không thể xuất dữ liệu!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }

    // Tạo POI - GET
    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm Địa điểm";
        return View();
    }

    // Tạo POI - POST
    [HttpPost]
    public async Task<IActionResult> Create(POI poi, IFormFile? imageFile)
    {
        try
        {
            // Upload ảnh nếu có
            if (imageFile != null && imageFile.Length > 0)
            {
                poi.ImageUrl = await _fileUploadService.UploadImageAsync(imageFile, "poi");
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
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return View(poi);
        }
    }

    // Sửa POI - GET
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/POI/{id}");

            if (response.IsSuccessStatusCode)
            {
                var poi = await response.Content.ReadFromJsonAsync<POI>();
                ViewData["Title"] = "Chỉnh sửa Địa điểm";
                return View(poi);
            }
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    // Sửa POI - POST
    [HttpPost]
    public async Task<IActionResult> Edit(int id, POI poi, IFormFile? imageFile)
    {
        try
        {
            // Upload ảnh nếu có
            if (imageFile != null && imageFile.Length > 0)
            {
                poi.ImageUrl = await _fileUploadService.UploadImageAsync(imageFile, "poi");
            }

            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PutAsJsonAsync($"api/POI/{id}", poi);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Update", "POI", poi.Name, poi.Name);
                TempData["success"] = "Cập nhật địa điểm thành công!";
                return RedirectToAction(nameof(Index));
            }

            TempData["error"] = "Lỗi khi cập nhật địa điểm!";
            return View(poi);
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return View(poi);
        }
    }

    // Xóa POI
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");

            // Lấy info trước khi xóa
            var getResponse = await client.GetAsync($"api/POI/{id}");
            string poiName = "Unknown";
            if (getResponse.IsSuccessStatusCode)
            {
                var poi = await getResponse.Content.ReadFromJsonAsync<POI>();
                poiName = poi?.Name ?? "Unknown";
            }

            var response = await client.DeleteAsync($"api/POI/{id}");

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Delete", "POI", poiName, null);
                TempData["success"] = "Xóa địa điểm thành công!";
            }
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }
}
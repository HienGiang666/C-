using Microsoft.AspNetCore.Mvc;
using QRCoder;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class POIController : Controller
{
    private const int PageSize = 12;
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;
    private readonly IFileUploadService _fileUploadService;

    public POIController(IHttpClientFactory clientFactory, IActivityLogger activityLogger, IFileUploadService fileUploadService)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
        _fileUploadService = fileUploadService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        ViewData["Title"] = "Quản lý địa điểm";
        page = Math.Max(1, page);
        var client = _clientFactory.CreateClient("TourApi");
        var url = BuildPoiListUrl();
        var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
            return View(new List<POI>());

        var all = await response.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
        all = all.OrderBy(p => p.Code).ThenBy(p => p.Id).ToList();

        var ownerNames = await LoadOwnerNamesAsync(client);

        var total = all.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (page > totalPages)
            page = totalPages;
        var slice = all.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        ViewBag.OwnerNames = ownerNames;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;
        return View(slice);
    }

    private async Task<Dictionary<int, string>> LoadOwnerNamesAsync(HttpClient client)
    {
        var map = new Dictionary<int, string>();
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(HttpContext.Session.GetString("UserId"), out var uid))
        {
            map[uid] = HttpContext.Session.GetString("FullName") ?? "—";
            return map;
        }

        var resp = await client.GetAsync("api/user");
        if (!resp.IsSuccessStatusCode)
            return map;
        var users = await resp.Content.ReadFromJsonAsync<List<User>>() ?? new List<User>();
        foreach (var u in users)
            map[u.Id] = u.FullName;
        return map;
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
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["error"] = "Chỉ chủ quán mới được thêm địa điểm mới. Vui lòng dùng trang Phê duyệt để kích hoạt POI sau khi chủ quán gửi.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = "Thêm địa điểm";
        return View(new POI { Radius = 20, Priority = 1, Rating = 4.5, IsActive = true });
    }

    [HttpPost]
    public async Task<IActionResult> Create(POI poi, IFormFile? uploadImage)
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["error"] = "Chỉ chủ quán mới được thêm địa điểm mới.";
            return RedirectToAction(nameof(Index));
        }

        if (!ValidatePoiRequired(poi, uploadImage, isCreate: true, out var err))
        {
            TempData["error"] = err;
            ViewData["Title"] = "Thêm địa điểm";
            return View(poi);
        }

        // Check duplicate coordinates
        var client = _clientFactory.CreateClient("TourApi");
        var dupMsg = await CheckDuplicateCoordinatesAsync(client, poi.Latitude, poi.Longitude);
        if (dupMsg != null)
        {
            TempData["error"] = dupMsg;
            ViewData["Title"] = "Thêm địa điểm";
            return View(poi);
        }

        if (uploadImage != null)
            poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");

        poi.Radius = 20;
        poi.Priority = 1;
        ApplyOwnershipForSave(poi, isNew: true);

        var response = await client.PostAsJsonAsync("api/POI", poi);

        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Create", "POI", null, poi.Name);
            TempData["success"] = "Đã gửi yêu cầu thêm địa điểm. Chờ Admin phê duyệt.";
            return RedirectToAction(nameof(Index));
        }
        TempData["error"] = "Lỗi khi thêm địa điểm!";
        ViewData["Title"] = "Thêm địa điểm";
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
        ViewData["Title"] = "Cập nhật địa điểm";
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

        // Load translations
        var transResponse = await client.GetAsync($"api/POI/{id}/translations");
        var translations = transResponse.IsSuccessStatusCode
            ? await transResponse.Content.ReadFromJsonAsync<List<POITranslation>>() ?? new List<POITranslation>()
            : new List<POITranslation>();
        ViewBag.Translations = translations;

        var isAdmin = (HttpContext.Session.GetString("Role") ?? "").Equals("Admin", StringComparison.OrdinalIgnoreCase);
        ViewBag.IsAdminPoiEdit = isAdmin;

        // Load categories
        var catResponse = await client.GetAsync("api/category");
        var allCategories = catResponse.IsSuccessStatusCode
            ? await catResponse.Content.ReadFromJsonAsync<List<Category>>() ?? new List<Category>()
            : new List<Category>();
        ViewBag.AllCategories = allCategories;

        var poiCatResponse = await client.GetAsync($"api/poi/{id}/categories");
        var poiCategories = poiCatResponse.IsSuccessStatusCode
            ? await poiCatResponse.Content.ReadFromJsonAsync<List<Category>>() ?? new List<Category>()
            : new List<Category>();
        ViewBag.PoiCategoryIds = poiCategories.Select(c => c.Id).ToList();

        return View(poi);
    }

    [HttpPost]
    public async Task<IActionResult> SaveTranslation(int id, string language, string? translatedName, string? translatedDescription)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var translation = new POITranslation
        {
            POIId = id,
            Language = language,
            Name = translatedName,
            Description = translatedDescription
        };
        var response = await client.PostAsJsonAsync($"api/POI/{id}/translations", translation);
        if (response.IsSuccessStatusCode)
            TempData["success"] = $"Đã lưu bản dịch [{language}] thành công!";
        else
            TempData["error"] = "Lỗi khi lưu bản dịch!";
        return RedirectToAction(nameof(Edit), new { id });
    }

    public async Task<IActionResult> DeleteTranslation(int id, string lang)
    {
        var client = _clientFactory.CreateClient("TourApi");
        await client.DeleteAsync($"api/POI/{id}/translations/{lang}");
        TempData["success"] = $"Đã xóa bản dịch [{lang}].";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SaveAllTranslations(int id, [FromForm(Name = "langs[]")] string[] langs, [FromForm(Name = "names[]")] string[] names, [FromForm(Name = "descs[]")] string[] descs)
    {
        var client = _clientFactory.CreateClient("TourApi");
        int saved = 0;
        for (int i = 0; i < langs.Length; i++)
        {
            var lang = langs[i];
            var name = i < names.Length ? names[i] : "";
            var desc = i < descs.Length ? descs[i] : "";

            if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(desc))
                continue;

            var translation = new POITranslation
            {
                POIId = id,
                Language = lang,
                Name = name?.Trim(),
                Description = desc?.Trim()
            };
            await client.PostAsJsonAsync($"api/POI/{id}/translations", translation);
            saved++;
        }

        TempData["success"] = $"Đã lưu {saved} bản dịch thành công!";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> SaveCategories(int id, int[] categoryIds)
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

        var response = await client.PutAsJsonAsync($"api/poi/{id}/categories", categoryIds ?? Array.Empty<int>());
        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Update", "POICategory", null, existing?.Name);
            TempData["success"] = "Cập nhật danh mục thành công!";
        }
        else
        {
            TempData["error"] = "Lỗi khi cập nhật danh mục!";
        }
        return RedirectToAction(nameof(Edit), new { id });
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

        var role = HttpContext.Session.GetString("Role") ?? "";
        var isAdmin = role.Equals("Admin", StringComparison.OrdinalIgnoreCase);

        if (isAdmin)
        {
            if (existing == null)
                return RedirectToAction(nameof(Index));
            if (poi.Radius <= 0 || poi.Priority <= 0 || poi.Rating <= 0)
            {
                TempData["error"] = "Bán kính, độ ưu tiên và đánh giá phải lớn hơn 0.";
                ViewBag.IsAdminPoiEdit = true;
                return View(poi);
            }
            poi.Id = id;
            poi.Name = existing.Name;
            poi.Address = existing.Address;
            poi.Latitude = existing.Latitude;
            poi.Longitude = existing.Longitude;
            poi.OpenTime = existing.OpenTime;
            poi.Description = existing.Description;
            poi.ImageUrl = existing.ImageUrl;
            poi.OwnerUserId = existing.OwnerUserId;
            poi.ApprovalStatus = existing.ApprovalStatus;
            poi.Code = existing.Code;
        }
        else
        {
            if (!ValidatePoiRequired(poi, uploadImage, isCreate: false, out var err))
            {
                TempData["error"] = err;
                ViewBag.IsAdminPoiEdit = false;
                return View(poi);
            }
            if (uploadImage != null)
                poi.ImageUrl = await _fileUploadService.UploadImageAsync(uploadImage, "images");
            else if (existing != null)
                poi.ImageUrl = existing.ImageUrl;

            poi.Id = id;
            poi.Radius = 20;
            poi.Priority = 1;
            if (existing != null)
            {
                poi.ApprovalStatus = existing.ApprovalStatus;
                if (existing.ApprovalStatus == "Approved")
                    poi.ApprovalStatus = "Pending";
            }
            ApplyOwnershipForSave(poi, isNew: false, existing);
        }

        var response = await client.PutAsJsonAsync($"api/POI/{id}", poi);

        if (response.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Update", "POI", null, poi.Name);
            TempData["success"] = "Cập nhật địa điểm thành công!";
            return RedirectToAction(nameof(Index));
        }
        TempData["error"] = "Lỗi khi cập nhật!";
        ViewBag.IsAdminPoiEdit = isAdmin;
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

    private async Task<string?> CheckDuplicateCoordinatesAsync(HttpClient client, double lat, double lng, int? excludePoiId = null)
    {
        try
        {
            var resp = await client.GetAsync("api/POI");
            if (!resp.IsSuccessStatusCode) return null;
            var allPois = await resp.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
            // ~10m tolerance ≈ 0.0001 degrees
            const double tolerance = 0.0001;
            var dup = allPois.FirstOrDefault(p =>
                (excludePoiId == null || p.Id != excludePoiId) &&
                Math.Abs(p.Latitude - lat) < tolerance &&
                Math.Abs(p.Longitude - lng) < tolerance);
            if (dup != null)
                return $"Vị trí này đã tồn tại POI \"{dup.Name}\" ({dup.DisplayCode}). Vui lòng chọn vị trí khác trên bản đồ.";
        }
        catch { }
        return null;
    }

    private static bool ValidatePoiRequired(POI poi, IFormFile? uploadImage, bool isCreate, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(poi.Name)) { error = "Tên địa điểm không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(poi.Address)) { error = "Địa chỉ không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(poi.OpenTime)) { error = "Giờ mở cửa không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(poi.Description)) { error = "Mô tả không được để trống."; return false; }
        if (isCreate && (uploadImage == null || uploadImage.Length == 0)) { error = "Vui lòng chọn ảnh đại diện."; return false; }
        if (poi.Latitude == 0 || poi.Longitude == 0) { error = "Vĩ độ và kinh độ phải khác 0."; return false; }
        if (poi.Rating <= 0) { error = "Đánh giá phải lớn hơn 0."; return false; }
        return true;
    }

    /// <summary>
    /// Generate QR Code cho POI - App quét sẽ mở trang map với POI detail + phát audio
    /// </summary>
    [HttpGet("POI/GenerateQR/{id}")]
    public async Task<IActionResult> GenerateQR(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync($"api/POI/{id}");

        if (!response.IsSuccessStatusCode)
            return NotFound(new { error = "POI not found" });

        var poi = await response.Content.ReadFromJsonAsync<POI>();
        if (poi == null)
            return NotFound(new { error = "POI not found" });

        // URL download app kèm POI ID để app quét vẫn nhận diện được
        const string downloadUrl = "https://github.com/HienGiang666/C-/releases";
        var qrContent = $"{downloadUrl}?poi={id}";

        // Tạo QR code với ECCLevel M (medium) thay vì Q để QR nhỏ hơn
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new PngByteQRCode(qrData);
        var qrBytes = qrCode.GetGraphic(10);

        var base64Image = Convert.ToBase64String(qrBytes);

        return Ok(new {
            qrCode = $"data:image/png;base64,{base64Image}",
            poiId = id,
            poiName = poi.Name,
            deepLink = qrContent,
            downloadUrl
        });
    }
}

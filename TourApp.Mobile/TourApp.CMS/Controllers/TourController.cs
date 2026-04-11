using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class TourController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    public TourController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý Tour";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/tour");

            if (response.IsSuccessStatusCode)
            {
                var tours = await response.Content.ReadFromJsonAsync<List<Tour>>();
                return View(tours ?? new List<Tour>());
            }
        }
        catch { }
        return View(new List<Tour>());
    }

    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "Thêm Tour mới";
        await LoadPoiSelectListAsync();
        return View(new TourFormViewModel
        {
            Tour = new Tour { IsActive = true, Duration = 1, Destination = string.Empty },
            RestaurantCount = 1
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(TourFormViewModel vm)
    {
        await LoadPoiSelectListAsync();
        vm.Tour.Destination = string.Empty;
        SanitizeTourNumbers(vm.Tour);
        NormalizeStops(vm);
        if (!ValidateTourCreateForm(vm, out var tourErr))
        {
            TempData["error"] = tourErr;
            return View(vm);
        }

        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/tour", vm.Tour);

            if (!response.IsSuccessStatusCode)
            {
                TempData["error"] = "Lỗi khi thêm tour!";
                return View(vm);
            }

            var created = await response.Content.ReadFromJsonAsync<Tour>();
            var tourId = created?.Id ?? 0;
            if (tourId == 0)
            {
                TempData["error"] = "Không lấy được ID tour sau khi tạo.";
                return View(vm);
            }

            if (vm.StopPoiIds.Count > 0)
            {
                var put = await client.PutAsJsonAsync($"api/tour/{tourId}/stops", vm.StopPoiIds.ToArray());
                if (!put.IsSuccessStatusCode)
                    TempData["error"] = "Tour đã tạo nhưng lưu danh sách điểm dừng thất bại.";
            }

            _activityLogger.LogActivity(HttpContext, "Create", "Tour", null, vm.Tour.Name);
            TempData["success"] = "Thêm tour thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch { }
        TempData["error"] = "Lỗi khi thêm tour!";
        return View(vm);
    }

    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Chỉnh sửa Tour";
        await LoadPoiSelectListAsync();
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/tour/{id}");
            if (!response.IsSuccessStatusCode)
                return RedirectToAction(nameof(Index));

            var tour = await response.Content.ReadFromJsonAsync<Tour>();
            if (tour == null)
                return RedirectToAction(nameof(Index));

            var stopsResp = await client.GetAsync($"api/tour/{id}/stops");
            var stopIds = new List<int>();
            if (stopsResp.IsSuccessStatusCode)
            {
                var raw = await stopsResp.Content.ReadAsStringAsync();
                using var j = JsonDocument.Parse(string.IsNullOrWhiteSpace(raw) ? "[]" : raw);
                foreach (var el in j.RootElement.EnumerateArray())
                {
                    if (el.TryGetProperty("poiId", out var pid))
                        stopIds.Add(pid.GetInt32());
                    else if (el.TryGetProperty("POIId", out var pid2))
                        stopIds.Add(pid2.GetInt32());
                }
            }

            var vm = new TourFormViewModel
            {
                Tour = tour,
                RestaurantCount = stopIds.Count,
                StopPoiIds = stopIds
            };
            return View(vm);
        }
        catch
        {
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, TourFormViewModel vm)
    {
        await LoadPoiSelectListAsync();
        vm.Tour.Id = id;
        SanitizeTourNumbers(vm.Tour);
        NormalizeStops(vm);

        if (!ModelState.IsValid)
            return View(vm);

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PutAsJsonAsync($"api/tour/{id}", vm.Tour);
            if (!response.IsSuccessStatusCode)
            {
                TempData["error"] = "Lỗi khi cập nhật tour!";
                return View(vm);
            }

            var put = await client.PutAsJsonAsync($"api/tour/{id}/stops", vm.StopPoiIds.ToArray());
            if (!put.IsSuccessStatusCode)
                TempData["error"] = "Cập nhật tour OK nhưng lưu điểm dừng thất bại.";
            else
                TempData["success"] = "Cập nhật tour thành công!";

            _activityLogger.LogActivity(HttpContext, "Update", "Tour", null, vm.Tour.Name);
            return RedirectToAction(nameof(Index));
        }
        catch { }
        TempData["error"] = "Lỗi khi cập nhật tour!";
        return View(vm);
    }

    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var getResp = await client.GetAsync($"api/tour/{id}");
            string? name = null;
            if (getResp.IsSuccessStatusCode)
            {
                var t = await getResp.Content.ReadFromJsonAsync<Tour>();
                name = t?.Name;
            }

            var response = await client.DeleteAsync($"api/tour/{id}");
            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Delete", "Tour", name, null);
                TempData["success"] = "Xóa tour thành công!";
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }

    private async Task LoadPoiSelectListAsync()
    {
        var client = _clientFactory.CreateClient("TourApi");
        var role = HttpContext.Session.GetString("Role") ?? "";
        var userId = HttpContext.Session.GetString("UserId");
        string url = "api/POI";
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) && int.TryParse(userId, out var oid))
            url += $"?ownerUserId={oid}";

        var resp = await client.GetAsync(url);
        var pois = resp.IsSuccessStatusCode
            ? await resp.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>()
            : new List<POI>();
        ViewBag.PoiOptions = new SelectList(pois, "Id", "Name");
        ViewBag.PoisJson = JsonSerializer.Serialize(pois.Select(p => new { id = p.Id, name = p.Name }));
    }

    private static void SanitizeTourNumbers(Tour tour)
    {
        if (tour.Price < 0) tour.Price = 0;
        if (tour.Duration < 1) tour.Duration = 1;
        if (tour.MaxParticipants < 0) tour.MaxParticipants = 0;
    }

    private static void NormalizeStops(TourFormViewModel vm)
    {
        vm.StopPoiIds = (vm.StopPoiIds ?? new List<int>())
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        if (vm.RestaurantCount > 0 && vm.StopPoiIds.Count > vm.RestaurantCount)
            vm.StopPoiIds = vm.StopPoiIds.Take(vm.RestaurantCount).ToList();
    }

    private static bool ValidateTourCreateForm(TourFormViewModel vm, out string error)
    {
        error = "";
        var t = vm.Tour;
        if (string.IsNullOrWhiteSpace(t.Name)) { error = "Tên tour không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(t.Description)) { error = "Mô tả tour không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(t.ImageUrl)) { error = "URL ảnh cover không được để trống."; return false; }
        if (string.IsNullOrWhiteSpace(t.SearchKeywords)) { error = "Từ khóa tìm kiếm không được để trống."; return false; }
        if (t.Price <= 0) { error = "Giá vé phải lớn hơn 0."; return false; }
        if (t.Duration < 1 || t.Duration > 3) { error = "Thời lượng phải từ 1 đến 3 ngày."; return false; }
        if (t.MaxParticipants < 1) { error = "Số khách tối đa phải là số nguyên lớn hơn 0."; return false; }
        if (vm.RestaurantCount < 1) { error = "Số quán / điểm dừng phải lớn hơn 0."; return false; }
        if (vm.StopPoiIds.Count != vm.RestaurantCount) { error = "Vui lòng chọn đủ địa điểm cho từng điểm dừng."; return false; }
        return true;
    }
}

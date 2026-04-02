using Microsoft.AspNetCore.Mvc;
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

    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm Tour mới";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Tour tour)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/tour", tour);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Create", "Tour", null, tour.Name);
                TempData["success"] = "Thêm tour thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        TempData["error"] = "Lỗi khi thêm tour!";
        return View(tour);
    }

    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Chỉnh sửa Tour";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/tour/{id}");

            if (response.IsSuccessStatusCode)
            {
                var tour = await response.Content.ReadFromJsonAsync<Tour>();
                return View(tour);
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, Tour tour)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PutAsJsonAsync($"api/tour/{id}", tour);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Update", "Tour", null, tour.Name);
                TempData["success"] = "Cập nhật tour thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        TempData["error"] = "Lỗi khi cập nhật tour!";
        return View(tour);
    }

    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            // Get name for log
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
}

using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class TourController : Controller
{
    private readonly IHttpClientFactory _clientFactory;

    public TourController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    // Hiển thị danh sách tour
    public async Task<IActionResult> Index()
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/tour");

            if (response.IsSuccessStatusCode)
            {
                var tours = await response.Content.ReadFromJsonAsync<List<Tour>>();
                ViewData["Title"] = "Quản lý Tour";
                return View(tours ?? new List<Tour>());
            }
        }
        catch { }
        
        ViewData["Title"] = "Quản lý Tour";
        return View(new List<Tour>());
    }

    // Tạo tour - GET
    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm Tour mới";
        return View();
    }

    // Tạo tour - POST
    [HttpPost]
    public async Task<IActionResult> Create(Tour tour)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/tour", tour);

            if (response.IsSuccessStatusCode)
            {
                TempData["success"] = "Thêm tour thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        
        TempData["error"] = "Lỗi khi thêm tour!";
        return View(tour);
    }

    // Sửa tour - GET
    public async Task<IActionResult> Edit(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/tour/{id}");

            if (response.IsSuccessStatusCode)
            {
                var tour = await response.Content.ReadFromJsonAsync<Tour>();
                ViewData["Title"] = "Chỉnh sửa Tour";
                return View(tour);
            }
        }
        catch { }
        
        return RedirectToAction(nameof(Index));
    }

    // Sửa tour - POST
    [HttpPost]
    public async Task<IActionResult> Edit(int id, Tour tour)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PutAsJsonAsync($"api/tour/{id}", tour);

            if (response.IsSuccessStatusCode)
            {
                TempData["success"] = "Cập nhật tour thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        
        TempData["error"] = "Lỗi khi cập nhật tour!";
        return View(tour);
    }

    // Xóa tour
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.DeleteAsync($"api/tour/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["success"] = "Xóa tour thành công!";
            }
        }
        catch { }
        
        return RedirectToAction(nameof(Index));
    }
}

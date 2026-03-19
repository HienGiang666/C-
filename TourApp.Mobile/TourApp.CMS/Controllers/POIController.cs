using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class POIController : Controller
{
    private readonly IHttpClientFactory _clientFactory;

    // Nhận cái "điện thoại" từ Program.cs truyền vào
    public POIController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    // Hiển thị trang danh sách địa điểm
    public async Task<IActionResult> Index()
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync("api/POI");

        if (response.IsSuccessStatusCode)
        {
            var pois = await response.Content.ReadFromJsonAsync<List<POI>>();
            return View(pois);
        }
        return View(new List<POI>());
    }

    // Mở giao diện trang Thêm Mới (GET)
    public IActionResult Create()
    {
        return View();
    }

    // Nhận dữ liệu từ Form và gửi sang API để lưu vào DB (POST)
    [HttpPost]
    public async Task<IActionResult> Create(POI poi)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.PostAsJsonAsync("api/POI", poi);

        if (response.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }
        return View(poi);
    }

    // NÚT XÓA (DELETE)
    public async Task<IActionResult> Delete(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        await client.DeleteAsync($"api/POI/{id}");

        return RedirectToAction(nameof(Index));
    }

    // NÚT SỬA (Mở form GET)
    public async Task<IActionResult> Edit(int id)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.GetAsync($"api/POI/{id}");

        if (response.IsSuccessStatusCode)
        {
            var poi = await response.Content.ReadFromJsonAsync<POI>();
            return View(poi); // Mở form và điền sẵn dữ liệu cũ vào
        }
        return RedirectToAction(nameof(Index));
    }

    // NÚT LƯU SỬA (Gửi dữ liệu PUT)
    [HttpPost]
    public async Task<IActionResult> Edit(int id, POI poi)
    {
        var client = _clientFactory.CreateClient("TourApi");
        var response = await client.PutAsJsonAsync($"api/POI/{id}", poi);

        if (response.IsSuccessStatusCode)
        {
            return RedirectToAction(nameof(Index));
        }
        return View(poi);
    }
}
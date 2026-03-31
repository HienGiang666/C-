using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class BookingController : Controller
{
    private readonly IHttpClientFactory _clientFactory;

    public BookingController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    // Hiển thị danh sách booking
    public async Task<IActionResult> Index()
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/booking");

            if (response.IsSuccessStatusCode)
            {
                var bookings = await response.Content.ReadFromJsonAsync<List<Booking>>();
                ViewData["Title"] = "Quản lý Booking";
                return View(bookings ?? new List<Booking>());
            }
        }
        catch { }
        
        ViewData["Title"] = "Quản lý Booking";
        return View(new List<Booking>());
    }

    // Chi tiết booking
    public async Task<IActionResult> Details(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/booking/{id}");

            if (response.IsSuccessStatusCode)
            {
                var booking = await response.Content.ReadFromJsonAsync<Booking>();
                ViewData["Title"] = "Chi tiết Booking";
                return View(booking);
            }
        }
        catch { }
        
        return RedirectToAction(nameof(Index));
    }

    // Cập nhật trạng thái booking
    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var booking = new { Status = status };
            var response = await client.PutAsJsonAsync($"api/booking/{id}", booking);

            if (response.IsSuccessStatusCode)
            {
                TempData["success"] = "Cập nhật trạng thái thành công!";
            }
        }
        catch { }
        
        return RedirectToAction(nameof(Details), new { id });
    }

    // Xóa booking
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.DeleteAsync($"api/booking/{id}");

            if (response.IsSuccessStatusCode)
            {
                TempData["success"] = "Xóa booking thành công!";
            }
        }
        catch { }
        
        return RedirectToAction(nameof(Index));
    }
}

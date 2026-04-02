using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class BookingController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    public BookingController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Quản lý Booking";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/booking");

            if (response.IsSuccessStatusCode)
            {
                var bookings = await response.Content.ReadFromJsonAsync<List<Booking>>();
                return View(bookings ?? new List<Booking>());
            }
        }
        catch { }
        return View(new List<Booking>());
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Chi tiết Booking";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/booking/{id}");
            if (response.IsSuccessStatusCode)
            {
                var booking = await response.Content.ReadFromJsonAsync<Booking>();
                return View(booking);
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Tạo Booking mới";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(Booking booking)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/booking", booking);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Create", "Booking", null, $"Tour:{booking.TourId} User:{booking.UserId}");
                TempData["success"] = "Tạo booking thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        TempData["error"] = "Lỗi khi tạo booking!";
        return View(booking);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            // Get full booking first
            var getResp = await client.GetAsync($"api/booking/{id}");
            if (getResp.IsSuccessStatusCode)
            {
                var booking = await getResp.Content.ReadFromJsonAsync<Booking>();
                if (booking != null)
                {
                    var oldStatus = booking.Status;
                    booking.Status = status;
                    var response = await client.PutAsJsonAsync($"api/booking/{id}", booking);
                    if (response.IsSuccessStatusCode)
                    {
                        _activityLogger.LogActivity(HttpContext, "Update", "Booking", $"#BK-{id}: {oldStatus}", status);
                        TempData["success"] = "Cập nhật trạng thái thành công!";
                    }
                }
            }
        }
        catch { }
        return RedirectToAction(nameof(Details), new { id });
    }

    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.DeleteAsync($"api/booking/{id}");
            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Delete", "Booking", $"#BK-{id}", null);
                TempData["success"] = "Xóa booking thành công!";
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }
}

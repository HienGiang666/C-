using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Helpers;
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

    private static async Task<Dictionary<int, string>> LoadUserCodeMapAsync(HttpClient client)
    {
        var map = new Dictionary<int, string>();
        try
        {
            var response = await client.GetAsync("api/user");
            if (!response.IsSuccessStatusCode)
                return map;
            var users = await response.Content.ReadFromJsonAsync<List<User>>();
            if (users == null)
                return map;
            foreach (var u in users)
                map[u.Id] = u.DisplayCode;
        }
        catch { }
        return map;
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
                ViewBag.UserCodeByUserId = await LoadUserCodeMapAsync(client);
                var toursResp = await client.GetAsync("api/tour");
                var tours = toursResp.IsSuccessStatusCode
                    ? await toursResp.Content.ReadFromJsonAsync<List<Tour>>()
                    : null;
                ViewBag.TourCodeByTourId = (tours ?? new List<Tour>())
                    .ToDictionary(t => t.Id, t => t.DisplayCode);
                return View(bookings ?? new List<Booking>());
            }
        }
        catch { }
        ViewBag.UserCodeByUserId = new Dictionary<int, string>();
        ViewBag.TourCodeByTourId = new Dictionary<int, string>();
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
                if (booking == null)
                    return RedirectToAction(nameof(Index));

                var userResp = await client.GetAsync($"api/user/{booking.UserId}");
                if (userResp.IsSuccessStatusCode)
                {
                    var user = await userResp.Content.ReadFromJsonAsync<User>();
                    if (user != null)
                        ViewBag.UserBadge = DisplayIdHelper.UserBadge(user);
                }

                var tourResp = await client.GetAsync($"api/tour/{booking.TourId}");
                if (tourResp.IsSuccessStatusCode)
                {
                    var tr = await tourResp.Content.ReadFromJsonAsync<Tour>();
                    if (tr != null)
                        ViewBag.TourCode = tr.DisplayCode;
                }

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
                        _activityLogger.LogActivity(HttpContext, "Update", "Booking", $"{DisplayIdHelper.BookingRef(id)}: {oldStatus}", status);
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
                _activityLogger.LogActivity(HttpContext, "Delete", "Booking", DisplayIdHelper.BookingRef(id), null);
                TempData["success"] = "Xóa booking thành công!";
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }
}

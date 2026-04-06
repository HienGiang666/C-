using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _clientFactory;

    public HomeController(IHttpClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        int poiCount = 0, tourCount = 0, userCount = 0, bookingCount = 0;

        try
        {
            var client = _clientFactory.CreateClient("TourApi");

            var poiTask     = client.GetAsync("api/POI");
            var tourTask    = client.GetAsync("api/tour");
            var userTask    = client.GetAsync("api/user");
            var bookingTask = client.GetAsync("api/booking");

            await Task.WhenAll(poiTask, tourTask, userTask, bookingTask);

            if (poiTask.Result.IsSuccessStatusCode)
            {
                var pois = await poiTask.Result.Content.ReadFromJsonAsync<List<POI>>();
                poiCount = pois?.Count ?? 0;
            }
            if (tourTask.Result.IsSuccessStatusCode)
            {
                var tours = await tourTask.Result.Content.ReadFromJsonAsync<List<Tour>>();
                tourCount = tours?.Count ?? 0;
            }
            if (userTask.Result.IsSuccessStatusCode)
            {
                var users = await userTask.Result.Content.ReadFromJsonAsync<List<User>>();
                userCount = users?.Count ?? 0;
            }
            if (bookingTask.Result.IsSuccessStatusCode)
            {
                var bookings = await bookingTask.Result.Content.ReadFromJsonAsync<List<Booking>>();
                bookingCount = bookings?.Count ?? 0;
            }
        }
        catch { }

        ViewBag.POICount     = poiCount;
        ViewBag.TourCount    = tourCount;
        ViewBag.UserCount    = userCount;
        ViewBag.BookingCount = bookingCount;

        return View();
    }

    /// <summary>
    /// Proxy /api/narrationlog/stats → Dashboard AJAX gọi qua CMS để bypass CORS.
    /// </summary>
    [HttpGet("/api/narrationlog/stats")]
    public async Task<IActionResult> NarrationStats()
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var r = await client.GetAsync("api/narrationlog/stats");
            if (r.IsSuccessStatusCode)
            {
                var json = await r.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
        }
        catch { }
        return Ok(new { total = 0, topPoi = Array.Empty<object>(), triggerStats = Array.Empty<object>(), dailyPlays = Array.Empty<object>() });
    }

    public IActionResult Privacy()
    {
        ViewData["Title"] = "Chính sách bảo mật";
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class HomeController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public HomeController(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";

        int poiCount = 0, tourCount = 0, userCount = 0, bookingCount = 0;
        var narrationStats = new NarrationStatsViewModel();
        var userLocationStats = new UserLocationStatsViewModel();
        var durationStats = new DurationStatsViewModel();

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            var role = HttpContext.Session.GetString("Role") ?? "";
            var userIdStr = HttpContext.Session.GetString("UserId");
            var poiUrl = "api/POI";
            if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(userIdStr, out var ownerId))
                poiUrl = $"api/POI?ownerUserId={ownerId}";

            // Basic counts
            var poiTask = client.GetAsync(poiUrl);
            var tourTask = client.GetAsync("api/tour");
            var userTask = client.GetAsync("api/user");
            var bookingTask = client.GetAsync("api/booking");

            // Analytics data
            var narrationStatsTask = client.GetAsync("api/narrationlog/stats");
            var userLocationStatsTask = client.GetAsync("api/userlocation/stats");
            var durationStatsTask = client.GetAsync("api/narrationlog/duration-stats");

            await Task.WhenAll(poiTask, tourTask, userTask, bookingTask, 
                narrationStatsTask, userLocationStatsTask, durationStatsTask);

            if (poiTask.Result.IsSuccessStatusCode)
            {
                var pois = await poiTask.Result.Content.ReadFromJsonAsync<List<POI>>(options);
                poiCount = pois?.Count ?? 0;
            }
            if (tourTask.Result.IsSuccessStatusCode)
            {
                var tours = await tourTask.Result.Content.ReadFromJsonAsync<List<Tour>>(options);
                tourCount = tours?.Count ?? 0;
            }
            if (userTask.Result.IsSuccessStatusCode)
            {
                var users = await userTask.Result.Content.ReadFromJsonAsync<List<User>>(options);
                userCount = users?.Count ?? 0;
            }
            if (bookingTask.Result.IsSuccessStatusCode)
            {
                var bookings = await bookingTask.Result.Content.ReadFromJsonAsync<List<Booking>>(options);
                bookingCount = bookings?.Count ?? 0;
            }

            // Parse analytics data
            if (narrationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await narrationStatsTask.Result.Content.ReadFromJsonAsync<NarrationStatsResponse>(options);
                if (stats != null)
                {
                    narrationStats.TotalPlays = stats.total;
                    narrationStats.TopPOIs = stats.topPoi?.Select(p => new TopPOIViewModel 
                    { 
                        PoiId = p.poiId, 
                        PoiName = p.poiName, 
                        Count = p.count 
                    }).ToList() ?? new List<TopPOIViewModel>();
                }
            }

            if (userLocationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await userLocationStatsTask.Result.Content.ReadFromJsonAsync<UserLocationStatsResponse>(options);
                if (stats != null)
                {
                    userLocationStats.OnlineNow = stats.onlineNow;
                    userLocationStats.Active24h = stats.active24h;
                    userLocationStats.OnlineLocations = stats.onlineLocations?.Select(l => new OnlineLocationViewModel
                    {
                        DeviceId = l.deviceId,
                        Latitude = l.latitude,
                        Longitude = l.longitude,
                        Timestamp = l.timestamp,
                        SessionId = l.sessionId
                    }).ToList() ?? new List<OnlineLocationViewModel>();
                }
            }

            if (durationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await durationStatsTask.Result.Content.ReadFromJsonAsync<DurationStatsResponse>(options);
                if (stats != null)
                {
                    durationStats.GlobalAverageFormatted = stats.globalAverageFormatted;
                    durationStats.POIDurations = stats.poiDurations?.Select(p => new POIDurationViewModel
                    {
                        PoiId = p.poiId,
                        PoiName = p.poiName,
                        AvgDurationFormatted = p.avgDurationFormatted,
                        TotalListens = p.totalListens
                    }).ToList() ?? new List<POIDurationViewModel>();
                }
            }
        }
        catch { }

        ViewBag.POICount = poiCount;
        ViewBag.TourCount = tourCount;
        ViewBag.UserCount = userCount;
        ViewBag.BookingCount = bookingCount;
        ViewBag.NarrationStats = narrationStats;
        ViewBag.UserLocationStats = userLocationStats;
        ViewBag.DurationStats = durationStats;

        return View();
    }

    public async Task<IActionResult> ThongKe()
    {
        ViewData["Title"] = "Thống kê";

        var narrationStats = new NarrationStatsViewModel();
        var userLocationStats = new UserLocationStatsViewModel();
        var durationStats = new DurationStatsViewModel();

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Analytics data
            var narrationStatsTask = client.GetAsync("api/narrationlog/stats");
            var userLocationStatsTask = client.GetAsync("api/userlocation/stats");
            var durationStatsTask = client.GetAsync("api/narrationlog/duration-stats");

            await Task.WhenAll(narrationStatsTask, userLocationStatsTask, durationStatsTask);

            if (narrationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await narrationStatsTask.Result.Content.ReadFromJsonAsync<NarrationStatsResponse>(options);
                if (stats != null)
                {
                    narrationStats.TotalPlays = stats.total;
                    narrationStats.TopPOIs = stats.topPoi?.Select(p => new TopPOIViewModel 
                    { 
                        PoiId = p.poiId, 
                        PoiName = p.poiName, 
                        Count = p.count 
                    }).ToList() ?? new List<TopPOIViewModel>();
                }
            }

            if (userLocationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await userLocationStatsTask.Result.Content.ReadFromJsonAsync<UserLocationStatsResponse>(options);
                if (stats != null)
                {
                    userLocationStats.OnlineNow = stats.onlineNow;
                    userLocationStats.Active24h = stats.active24h;
                    userLocationStats.OnlineLocations = stats.onlineLocations?.Select(l => new OnlineLocationViewModel
                    {
                        DeviceId = l.deviceId,
                        Latitude = l.latitude,
                        Longitude = l.longitude,
                        Timestamp = l.timestamp,
                        SessionId = l.sessionId
                    }).ToList() ?? new List<OnlineLocationViewModel>();
                }
            }

            if (durationStatsTask.Result.IsSuccessStatusCode)
            {
                var stats = await durationStatsTask.Result.Content.ReadFromJsonAsync<DurationStatsResponse>(options);
                if (stats != null)
                {
                    durationStats.GlobalAverageFormatted = stats.globalAverageFormatted;
                    durationStats.POIDurations = stats.poiDurations?.Select(p => new POIDurationViewModel
                    {
                        PoiId = p.poiId,
                        PoiName = p.poiName,
                        AvgDurationFormatted = p.avgDurationFormatted,
                        TotalListens = p.totalListens
                    }).ToList() ?? new List<POIDurationViewModel>();
                }
            }
        }
        catch { }

        ViewBag.NarrationStats = narrationStats;
        ViewBag.UserLocationStats = userLocationStats;
        ViewBag.DurationStats = durationStats;
        ViewBag.ApiBaseUrl = _configuration.GetSection("TourApi:BaseUrl").Value?.TrimEnd('/') ?? "https://localhost:7244";

        return View("Statistics");
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

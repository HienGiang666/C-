using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers
{
    public class HomeController : Controller
    {
        private readonly IHttpClientFactory _clientFactory;

        public HomeController(IHttpClientFactory clientFactory)
        {
            _clientFactory = clientFactory;
        }

        public async Task<IActionResult> Index()
        {
            // Nếu chưa đăng nhập, redirect to login
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
                return RedirectToAction("Login", "Auth");

            ViewData["Title"] = "Dashboard";

            try
            {
                var client = _clientFactory.CreateClient("TourApi");

                // Lấy dữ liệu từ API
                var poisResponse = await client.GetAsync("api/POI");
                var toursResponse = await client.GetAsync("api/tour");
                var bookingsResponse = await client.GetAsync("api/booking");
                var usersResponse = await client.GetAsync("api/user");

                ViewBag.POICount = poisResponse.IsSuccessStatusCode 
                    ? (await poisResponse.Content.ReadFromJsonAsync<List<POI>>())?.Count ?? 0 
                    : 0;

                ViewBag.TourCount = toursResponse.IsSuccessStatusCode 
                    ? (await toursResponse.Content.ReadFromJsonAsync<List<Tour>>())?.Count ?? 0 
                    : 0;

                ViewBag.BookingCount = bookingsResponse.IsSuccessStatusCode 
                    ? (await bookingsResponse.Content.ReadFromJsonAsync<List<Booking>>())?.Count ?? 0 
                    : 0;

                ViewBag.UserCount = usersResponse.IsSuccessStatusCode 
                    ? (await usersResponse.Content.ReadFromJsonAsync<List<User>>())?.Count ?? 0 
                    : 0;
            }
            catch
            {
                ViewBag.POICount = 0;
                ViewBag.TourCount = 0;
                ViewBag.BookingCount = 0;
                ViewBag.UserCount = 0;
            }

            return View();
        }

        public IActionResult Privacy()
        {
            ViewData["Title"] = "Privacy Policy";
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}


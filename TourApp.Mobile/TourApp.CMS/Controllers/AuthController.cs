using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class AuthController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    public AuthController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("UserId") != null)
            return RedirectToAction("Index", "Home");
        ViewData["Title"] = "Đăng nhập";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/user/login", new
            {
                Username = model.Username,
                Password = model.Password
            });

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                HttpContext.Session.SetString("UserId", root.GetProperty("id").GetInt32().ToString());
                HttpContext.Session.SetString("Username", root.GetProperty("username").GetString() ?? "");
                HttpContext.Session.SetString("FullName", root.GetProperty("fullName").GetString() ?? "");
                HttpContext.Session.SetString("Role", root.GetProperty("role").GetString() ?? "");
                HttpContext.Session.SetString("Email", root.GetProperty("email").GetString() ?? "");

                _activityLogger.LogActivity(HttpContext, "Login", "Auth", null, model.Username);
                TempData["success"] = $"Xin chào {HttpContext.Session.GetString("FullName")}!";
                return RedirectToAction("Index", "Home");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng!");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Không thể kết nối đến máy chủ API. Vui lòng kiểm tra API đang chạy!");
            }
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Lỗi kết nối: Hãy đảm bảo TourApp.API đang chạy song song.");
            System.Diagnostics.Debug.WriteLine($"[Auth Login] {ex.Message}");
        }

        return View(model);
    }

    public IActionResult Logout()
    {
        var username = HttpContext.Session.GetString("Username");
        _activityLogger.LogActivity(HttpContext, "Logout", "Auth", username, null);
        HttpContext.Session.Clear();
        TempData["success"] = "Đã đăng xuất thành công!";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));
        ViewData["Title"] = "Hồ sơ cá nhân";
        return View();
    }
}

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
    public async Task<IActionResult> Login()
    {
        if (HttpContext.Session.GetString("UserId") != null)
            return RedirectToAction("Index", "Home");

        var token = Request.Cookies["AuthToken"];
        if (!string.IsNullOrEmpty(token))
        {
            try
            {
                var client = _clientFactory.CreateClient("TourApi");
                var response = await client.GetAsync($"api/user/{token}");
                if (response.IsSuccessStatusCode)
                {
                    var user = await response.Content.ReadFromJsonAsync<AdminUser>();
                    if (user != null && IsCmsAllowedRole(user.Role))
                    {
                        HttpContext.Session.SetString("UserId", user.Id.ToString());
                        HttpContext.Session.SetString("Username", user.Username);
                        HttpContext.Session.SetString("FullName", user.FullName);
                        HttpContext.Session.SetString("Role", NormalizeCmsRole(user.Role));
                        HttpContext.Session.SetString("Email", user.Email);
                        return RedirectToAction("Index", "Home");
                    }
                }
            }
            catch { }
        }

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
                
                var role = root.GetProperty("role").GetString() ?? "";
                if (!IsCmsAllowedRole(role))
                {
                    ModelState.AddModelError(string.Empty, "Chỉ dành cho Quản trị viên hoặc Chủ quán ăn. Khách hàng không đăng nhập CMS được.");
                    return View(model);
                }

                HttpContext.Session.SetString("UserId", root.GetProperty("id").GetInt32().ToString());
                HttpContext.Session.SetString("Username", root.GetProperty("username").GetString() ?? "");
                HttpContext.Session.SetString("FullName", root.GetProperty("fullName").GetString() ?? "");
                HttpContext.Session.SetString("Role", NormalizeCmsRole(role));
                HttpContext.Session.SetString("Email", root.GetProperty("email").GetString() ?? "");

                if (model.RememberMe)
                {
                    var cookieOptions = new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true };
                    Response.Cookies.Append("AuthToken", root.GetProperty("id").GetInt32().ToString(), cookieOptions);
                }

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
        Response.Cookies.Delete("AuthToken");
        TempData["success"] = "Đã đăng xuất thành công!";
        return RedirectToAction(nameof(Login));
    }

    public async Task<IActionResult> Profile()
    {
        var userId = HttpContext.Session.GetString("UserId");
        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));
            
        try 
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/user/{userId}");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<AdminUser>();
                ViewData["Title"] = "Hồ sơ cá nhân";
                return View(user);
            }
        }
        catch { }

        TempData["error"] = "Không thể tải thông tin hồ sơ từ máy chủ.";
        return RedirectToAction("Index", "Home");
    }

    private static bool IsCmsAllowedRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return false;
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return true;
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase))
            return true;
        if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string NormalizeCmsRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Customer";
        if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            return "RestaurantOwner";
        return role;
    }
}

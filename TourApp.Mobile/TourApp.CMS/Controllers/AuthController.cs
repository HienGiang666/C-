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
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(options);
                if (loginResponse == null)
                {
                    ModelState.AddModelError(string.Empty, "Dữ liệu trả về từ API không hợp lệ!");
                    return View(model);
                }

                var role = loginResponse.Role ?? "";
                if (!IsCmsAllowedRole(role))
                {
                    ModelState.AddModelError(string.Empty, "Chỉ dành cho Quản trị viên hoặc Chủ quán ăn. Khách hàng không đăng nhập CMS được.");
                    return View(model);
                }

                HttpContext.Session.SetString("UserId", loginResponse.Id.ToString());
                HttpContext.Session.SetString("Username", loginResponse.Username ?? "");
                HttpContext.Session.SetString("FullName", loginResponse.FullName ?? "");
                HttpContext.Session.SetString("Role", NormalizeCmsRole(role));
                HttpContext.Session.SetString("Email", loginResponse.Email ?? "");

                if (model.RememberMe)
                {
                    var cookieOptions = new CookieOptions { Expires = DateTime.Now.AddDays(30), HttpOnly = true };
                    Response.Cookies.Append("AuthToken", loginResponse.Id.ToString(), cookieOptions);
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
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var user = await response.Content.ReadFromJsonAsync<User>(options);
                ViewData["Title"] = "Hồ sơ cá nhân";
                return View(user);
            }
        }
        catch { }

        TempData["error"] = "Không thể tải thông tin hồ sơ từ máy chủ.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(string newPassword, string confirmPassword)
    { 
        if (string.IsNullOrEmpty(newPassword) || newPassword != confirmPassword)
        { 
            TempData["error"] = "Mật khẩu xác nhận không khớp!";
            return RedirectToAction(nameof(Profile));
        }

        var userId = HttpContext.Session.GetString("UserId");
        var username = HttpContext.Session.GetString("Username");

        if (username?.ToLower() == "admin")
        { 
            TempData["error"] = "Không được phép đổi mật khẩu Admin!";
            return RedirectToAction(nameof(Profile));
        }

        try
        { 
            var client = _clientFactory.CreateClient("TourApi");
            var userResponse = await client.GetAsync($"api/user/{userId}");
            if (!userResponse.IsSuccessStatusCode)
            { 
                TempData["error"] = "Lỗi kết nối!";
                return RedirectToAction(nameof(Profile));
            }

            var user = await userResponse.Content.ReadFromJsonAsync<AdminUser>();
            if (user == null) return RedirectToAction(nameof(Profile));

            // Chỉ gửi password mới (plain text), API sẽ hash
            user.PasswordHash = newPassword;

            var updateResponse = await client.PutAsJsonAsync($"api/user/{userId}", user);
            if (updateResponse.IsSuccessStatusCode)
            { 
                TempData["success"] = "Đổi mật khẩu thành công!";
            }
            else
            { 
                TempData["error"] = "Cập nhật thất bại!";
            }
        }
        catch (Exception ex)
        { 
            TempData["error"] = "Lỗi: " + ex.Message;
        }

        return RedirectToAction(nameof(Profile));
    }

    private bool IsCmsAllowedRole(string role)
    { 
        if (string.IsNullOrWhiteSpace(role)) return false;
        var r = role.ToLower();
        return r == "admin" || r == "restaurantowner" || r == "staff";
    }

    private string NormalizeCmsRole(string role)
    { 
        if (role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)) return "Chủ quán ăn";
        if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase)) return "Nhân viên";
        return role;
    }
}

public class AdminUser
{ 
    public int Id { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? PasswordHash { get; set; }
    public bool IsActive { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public record LoginResponse(int Id, string? Username, string? FullName, string? Email, string? Role);

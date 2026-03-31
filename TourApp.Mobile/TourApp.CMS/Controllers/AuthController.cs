using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using TourApp.CMS.Models;

namespace TourApp.CMS.Controllers;

public class AuthController : Controller
{
    // Temporary in-memory storage (Replace with database later)
    private static List<AdminUser> _users = new List<AdminUser>
    {
        new AdminUser
        {
            Id = 1,
            Username = "admin",
            Email = "admin@tourapp.com",
            PasswordHash = HashPassword("admin123"),
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true
        }
    };

    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        ViewData["Title"] = "Đăng nhập";
        return View();
    }

    [HttpPost]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = _users.FirstOrDefault(u => u.Username == model.Username && u.IsActive);
        
        if (user == null || !VerifyPassword(model.Password, user.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập hoặc mật khẩu không đúng!");
            return View(model);
        }

        // Set session
        HttpContext.Session.SetString("UserId", user.Id.ToString());
        HttpContext.Session.SetString("Username", user.Username);
        HttpContext.Session.SetString("FullName", user.FullName);
        HttpContext.Session.SetString("Role", user.Role);
        HttpContext.Session.SetString("Email", user.Email);

        // Update last login
        user.LastLogin = DateTime.Now;

        TempData["success"] = $"Xin chào {user.FullName}!";
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Register()
    {
        ViewData["Title"] = "Đăng ký";
        return View();
    }

    [HttpPost]
    public IActionResult Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        if (model.Password != model.ConfirmPassword)
        {
            ModelState.AddModelError(string.Empty, "Mật khẩu không khớp!");
            return View(model);
        }

        if (_users.Any(u => u.Username == model.Username))
        {
            ModelState.AddModelError(string.Empty, "Tên đăng nhập đã tồn tại!");
            return View(model);
        }

        var newUser = new AdminUser
        {
            Id = _users.Max(u => u.Id) + 1,
            Username = model.Username,
            Email = model.Email,
            PasswordHash = HashPassword(model.Password),
            FullName = model.FullName,
            Role = "Staff"
        };

        _users.Add(newUser);

        TempData["success"] = "Đăng ký thành công! Vui lòng đăng nhập.";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["success"] = "Đã đăng xuất thành công!";
        return RedirectToAction(nameof(Login));
    }

    public IActionResult Profile()
    {
        var userId = HttpContext.Session.GetString("UserId");
        var username = HttpContext.Session.GetString("Username");

        if (string.IsNullOrEmpty(userId))
            return RedirectToAction(nameof(Login));

        ViewData["Title"] = "Hồ sơ cá nhân";
        var user = _users.FirstOrDefault(u => u.Id == int.Parse(userId));
        return View(user);
    }

    // Helper methods
    private static string HashPassword(string password)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(hashedBytes);
        }
    }

    private static bool VerifyPassword(string password, string hash)
    {
        var hashOfInput = HashPassword(password);
        return hashOfInput == hash;
    }
}

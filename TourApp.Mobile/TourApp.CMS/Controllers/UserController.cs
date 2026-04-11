using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class UserController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    public UserController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    public async Task<IActionResult> Index(string search = "")
    {
        ViewData["Title"] = "Quản lý User";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/user");

            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<List<User>>();

                if (!string.IsNullOrEmpty(search))
                {
                    users = users?.Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                              u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList() ?? new List<User>();
                    ViewBag.SearchTerm = search;
                }
                return View(users ?? new List<User>());
            }
        }
        catch { }
        return View(new List<User>());
    }

    public async Task<IActionResult> Details(int id)
    {
        ViewData["Title"] = "Chi tiết User";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/user/{id}");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>();
                return View(user);
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm User mới";
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(User user)
    {
        if (user.Role.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(user.Role), "Khách hàng chỉ đăng ký qua app mobile, không tạo từ CMS.");
        }

        if (string.IsNullOrWhiteSpace(user.Username))
            ModelState.AddModelError(nameof(user.Username), "Vui lòng nhập tên đăng nhập.");
        if (string.IsNullOrWhiteSpace(user.PasswordHash))
            ModelState.AddModelError(nameof(user.PasswordHash), "Vui lòng nhập mật khẩu.");

        if (!ModelState.IsValid)
            return View(user);

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PostAsJsonAsync("api/user", user);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Create", "User", null, user.FullName);
                TempData["success"] = "Thêm user thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        TempData["error"] = "Lỗi khi thêm user!";
        return View(user);
    }

    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "Chỉnh sửa User";
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync($"api/user/{id}");
            if (response.IsSuccessStatusCode)
            {
                var user = await response.Content.ReadFromJsonAsync<User>();
                if (user != null && user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
                    user.Role = "RestaurantOwner";
                return View(user);
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, User user)
    {
        if (user.Role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            user.Role = "RestaurantOwner";

        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.PutAsJsonAsync($"api/user/{id}", user);

            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Update", "User", null, user.FullName);
                TempData["success"] = "Cập nhật user thành công!";
                return RedirectToAction(nameof(Index));
            }
        }
        catch { }
        TempData["error"] = "Lỗi cập nhật user!";
        return View(user);
    }

    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var client = _clientFactory.CreateClient("TourApi");
            // Get name for log
            var getResp = await client.GetAsync($"api/user/{id}");
            string? name = null;
            if (getResp.IsSuccessStatusCode)
            {
                var u = await getResp.Content.ReadFromJsonAsync<User>();
                name = u?.FullName;
            }

            var response = await client.DeleteAsync($"api/user/{id}");
            if (response.IsSuccessStatusCode)
            {
                _activityLogger.LogActivity(HttpContext, "Delete", "User", name, null);
                TempData["success"] = "Xóa user thành công!";
            }
        }
        catch { }
        return RedirectToAction(nameof(Index));
    }
}

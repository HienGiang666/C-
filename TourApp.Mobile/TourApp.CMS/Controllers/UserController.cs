using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class UserController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    // Mock data (Replace với database sau)
    private static List<User> _users = new List<User>
    {
        new User
        {
            Id = 1,
            FullName = "Nguyễn Văn An",
            Email = "an@example.com",
            PhoneNumber = "0901234567",
            Role = "Customer",
            Address = "123 Nguyễn Huệ, TPHCM",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-30)
        },
        new User
        {
            Id = 2,
            FullName = "Trần Thị B",
            Email = "thi.b@example.com",
            PhoneNumber = "0912345678",
            Role = "Staff",
            Address = "456 Lê Lợi, TPHCM",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-20)
        },
        new User
        {
            Id = 3,
            FullName = "Lê Văn C",
            Email = "van.c@example.com",
            PhoneNumber = "0923456789",
            Role = "Customer",
            Address = "789 Ngô Quyền, Hà Nội",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-15)
        },
        new User
        {
            Id = 4,
            FullName = "Hoàng Thị D",
            Email = "thi.d@example.com",
            PhoneNumber = "0934567890",
            Role = "Admin",
            Address = "321 Đại Cồ Việt, Hà Nội",
            IsActive = true,
            CreatedAt = DateTime.Now.AddDays(-10)
        },
        new User
        {
            Id = 5,
            FullName = "Phạm Văn E",
            Email = "van.e@example.com",
            PhoneNumber = "0945678901",
            Role = "Customer",
            Address = "654 Bạch Đằng, Đà Nẵng",
            IsActive = false,
            CreatedAt = DateTime.Now.AddDays(-5)
        }
    };

    public UserController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    // Hiển thị danh sách user
    public async Task<IActionResult> Index(string search = "")
    {
        try
        {
            // Thử lấy từ API trước
            var client = _clientFactory.CreateClient("TourApi");
            var response = await client.GetAsync("api/user");

            if (response.IsSuccessStatusCode)
            {
                var users = await response.Content.ReadFromJsonAsync<List<User>>();
                ViewData["Title"] = "Quản lý User";

                // Tìm kiếm
                if (!string.IsNullOrEmpty(search))
                {
                    users = users?.Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                              u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                                  .ToList() ?? new List<User>();
                    ViewBag.SearchTerm = search;
                }

                return View(users ?? new List<User>());
            }
        }
        catch { }

        // Nếu API fail, dùng mock data
        var mockUsers = _users;
        if (!string.IsNullOrEmpty(search))
        {
            mockUsers = _users.Where(u => u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                                          u.Email.Contains(search, StringComparison.OrdinalIgnoreCase))
                              .ToList();
            ViewBag.SearchTerm = search;
        }

        ViewData["Title"] = "Quản lý User";
        return View(mockUsers);
    }

    // Chi tiết user
    public IActionResult Details(int id)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                ViewData["Title"] = "Chi tiết User";
                return View(user);
            }
        }
        catch { }

        TempData["error"] = "Không tìm thấy user!";
        return RedirectToAction(nameof(Index));
    }

    // Tạo user - GET
    public IActionResult Create()
    {
        ViewData["Title"] = "Thêm User mới";
        return View();
    }

    // Tạo user - POST
    [HttpPost]
    public IActionResult Create(User user)
    {
        try
        {
            if (!ModelState.IsValid)
                return View(user);

            // Check email đã tồn tại
            if (_users.Any(u => u.Email == user.Email))
            {
                ModelState.AddModelError("Email", "Email đã tồn tại!");
                return View(user);
            }

            user.Id = _users.Max(u => u.Id) + 1;
            user.CreatedAt = DateTime.Now;
            user.IsActive = true;

            _users.Add(user);
            _activityLogger.LogActivity(HttpContext, "Create", "User", null, user.FullName);

            TempData["success"] = "Thêm user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return View(user);
        }
    }

    // Sửa user - GET
    public IActionResult Edit(int id)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                ViewData["Title"] = "Chỉnh sửa User";
                return View(user);
            }
        }
        catch { }

        TempData["error"] = "Không tìm thấy user!";
        return RedirectToAction(nameof(Index));
    }

    // Sửa user - POST
    [HttpPost]
    public IActionResult Edit(int id, User user)
    {
        try
        {
            if (!ModelState.IsValid)
                return View(user);

            var existingUser = _users.FirstOrDefault(u => u.Id == id);
            if (existingUser == null)
                return RedirectToAction(nameof(Index));

            // Check email không trùng với user khác
            if (_users.Any(u => u.Email == user.Email && u.Id != id))
            {
                ModelState.AddModelError("Email", "Email đã tồn tại!");
                return View(user);
            }

            existingUser.FullName = user.FullName;
            existingUser.Email = user.Email;
            existingUser.PhoneNumber = user.PhoneNumber;
            existingUser.Address = user.Address;
            existingUser.Role = user.Role;
            existingUser.IsActive = user.IsActive;

            _activityLogger.LogActivity(HttpContext, "Update", "User", existingUser.FullName, user.FullName);

            TempData["success"] = "Cập nhật user thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return View(user);
        }
    }

    // Xóa user
    public IActionResult Delete(int id)
    {
        try
        {
            var user = _users.FirstOrDefault(u => u.Id == id);
            if (user != null)
            {
                _users.Remove(user);
                _activityLogger.LogActivity(HttpContext, "Delete", "User", user.FullName, null);
                TempData["success"] = "Xóa user thành công!";
            }
        }
        catch { }

        return RedirectToAction(nameof(Index));
    }

    // Export users
    public IActionResult Export()
    {
        try
        {
            var excelData = new ExportService(new Microsoft.Extensions.Logging.LoggerFactory().CreateLogger<ExportService>())
                .ExportToExcel(_users, "Users");

            _activityLogger.LogActivity(HttpContext, "Export", "User", null, $"{_users.Count} items");
            return File(excelData, "application/vnd.ms-excel", $"Users_{DateTime.Now:yyyyMMdd_HHmmss}.xls");
        }
        catch (Exception ex)
        {
            TempData["error"] = $"Lỗi: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }
    }
}

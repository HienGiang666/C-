using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class ActivityController : Controller
{
    private const int PageSize = 10;
    private readonly IActivityLogger _activityLogger;

    public ActivityController(IActivityLogger activityLogger)
    {
        _activityLogger = activityLogger;
    }

    public IActionResult Index(int page = 1)
    {
        ViewData["Title"] = "Lịch sử hoạt động";
        var role = HttpContext.Session.GetString("Role") ?? "";
        var username = HttpContext.Session.GetString("Username") ?? "";
        page = Math.Max(1, page);
        var pageIndex0 = page - 1;

        var (items, total) = role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)
            ? _activityLogger.GetLogsPaged(HttpContext, pageIndex0, PageSize, username)
            : _activityLogger.GetLogsPaged(HttpContext, pageIndex0, PageSize, null);

        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        if (page > totalPages && totalPages > 0)
            return RedirectToAction(nameof(Index), new { page = totalPages });

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalCount = total;
        return View(items.ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ClearHistory()
    {
        _activityLogger.ClearVisibleHistory(HttpContext);
        TempData["success"] = "Đã ẩn nhật ký hiển thị đến thời điểm này. Các thao tác hệ thống vẫn được ghi nhận bình thường.";
        return RedirectToAction(nameof(Index));
    }
}

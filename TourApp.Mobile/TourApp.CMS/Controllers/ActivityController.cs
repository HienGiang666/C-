using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

public class ActivityController : Controller
{
    private readonly IActivityLogger _activityLogger;

    public ActivityController(IActivityLogger activityLogger)
    {
        _activityLogger = activityLogger;
    }

    public IActionResult Index()
    {
        ViewData["Title"] = "Lịch sử hoạt động";
        var role = HttpContext.Session.GetString("Role") ?? "";
        var username = HttpContext.Session.GetString("Username") ?? "";
        var logs = role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase)
            ? _activityLogger.GetLogsForUser(username, 200)
            : _activityLogger.GetLogs(200);
        return View(logs);
    }
}

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
        var logs = _activityLogger.GetLogs(200);
        return View(logs);
    }
}

using Microsoft.AspNetCore.Mvc;
using TourApp.CMS.Models;
using TourApp.CMS.Services;

namespace TourApp.CMS.Controllers;

/// <summary>Chỉ Admin: phê duyệt POI do chủ quán gửi.</summary>
public class ApprovalController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IActivityLogger _activityLogger;

    public ApprovalController(IHttpClientFactory clientFactory, IActivityLogger activityLogger)
    {
        _clientFactory = clientFactory;
        _activityLogger = activityLogger;
    }

    public async Task<IActionResult> Index()
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        ViewData["Title"] = "Phê duyệt địa điểm";
        var client = _clientFactory.CreateClient("TourApi");
        var resp = await client.GetAsync("api/POI/pending");
        if (!resp.IsSuccessStatusCode)
            return View(new List<POI>());

        var list = await resp.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
        return View(list);
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int id)
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        var client = _clientFactory.CreateClient("TourApi");
        var resp = await client.PostAsync($"api/POI/{id}/approve", null);
        if (resp.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Approve", "POI", null, id.ToString());
            TempData["success"] = "Đã phê duyệt địa điểm.";
        }
        else
            TempData["error"] = "Không phê duyệt được.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Reject(int id)
    {
        if (!IsAdmin())
            return RedirectToAction("Index", "Home");

        var client = _clientFactory.CreateClient("TourApi");
        var resp = await client.PostAsync($"api/POI/{id}/reject", null);
        if (resp.IsSuccessStatusCode)
        {
            _activityLogger.LogActivity(HttpContext, "Reject", "POI", null, id.ToString());
            TempData["success"] = "Đã từ chối địa điểm.";
        }
        else
            TempData["error"] = "Không từ chối được.";

        return RedirectToAction(nameof(Index));
    }

    private bool IsAdmin()
    {
        return string.Equals(HttpContext.Session.GetString("Role"), "Admin", StringComparison.OrdinalIgnoreCase);
    }
}

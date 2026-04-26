using Microsoft.AspNetCore.Mvc;

namespace TourApp.CMS.Controllers;

public class ActivityManagementController : Controller
{
    private readonly IHttpClientFactory _clientFactory;
    private readonly IConfiguration _configuration;

    public ActivityManagementController(IHttpClientFactory clientFactory, IConfiguration configuration)
    {
        _clientFactory = clientFactory;
        _configuration = configuration;
    }

    public IActionResult Index()
    {
        var role = HttpContext.Session.GetString("Role") ?? "";
        if (!role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
            return RedirectToAction("Index", "Home");

        ViewData["Title"] = "Quản lý hoạt động";
        ViewBag.ApiBaseUrl = _configuration.GetSection("TourApi:BaseUrl").Value?.TrimEnd('/') ?? "https://localhost:7244";
        return View();
    }
}

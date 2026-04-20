using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TourApp.CMS.Filters;

public class CmsAccessFilter : IActionFilter
{
    public void OnActionExecuting(ActionExecutingContext context)
    {
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Skip auth and translate API
        if (path.StartsWith("/auth", StringComparison.Ordinal) ||
            path.StartsWith("/api/translate", StringComparison.Ordinal))
            return;

        if (string.IsNullOrEmpty(context.HttpContext.Session.GetString("UserId")))
            return;

        var role = context.HttpContext.Session.GetString("Role") ?? "";
        if (role.Equals("Customer", StringComparison.OrdinalIgnoreCase))
        {
            context.HttpContext.Session.Clear();
            context.Result = new RedirectToActionResult("Login", "Auth", null);
            return;
        }

        if (!role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase))
            return;

        var ctrl = context.RouteData.Values["controller"]?.ToString() ?? "";
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Home", "POI", "Audio", "Activity", "Auth", "Translate"
        };

        if (allowed.Contains(ctrl))
            return;

        context.Result = new RedirectToActionResult("Index", "Home", null);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TourApp.CMS.Filters
{
    public class AuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var path = context.HttpContext.Request.Path.Value?.ToLower() ?? "";

            // Allow Auth and Translate API without login redirect
            if (path.StartsWith("/auth") || path.StartsWith("/api/translate"))
            {
                base.OnActionExecuting(context);
                return;
            }

            var userId = context.HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                context.Result = new RedirectToActionResult("Login", "Auth", null);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
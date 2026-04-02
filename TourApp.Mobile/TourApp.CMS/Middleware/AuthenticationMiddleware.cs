namespace TourApp.CMS.Middleware
{
    public class AuthenticationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthenticationMiddleware> _logger;

        // Routes không cần authentication
        private static readonly string[] PublicRoutes = new[]
        {
            "/",
            "/Auth/Login",
            "/Auth/Register",
            "/css",
            "/js",
            "/lib",
            "/images",
            "/uploads"
        };

        public AuthenticationMiddleware(RequestDelegate next, ILogger<AuthenticationMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

            // Kiểm tra xem có phải route public không
            if (IsPublicRoute(path))
            {
                await _next(context);
                return;
            }

            // Kiểm tra session
            var userId = context.Session.GetString("UserId");
            var username = context.Session.GetString("Username");

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
            {
                _logger.LogWarning($"Unauthorized access attempt to {path}");
                context.Response.Redirect("/Auth/Login");
                return;
            }

            _logger.LogInformation($"User {username} accessed {path}");
            await _next(context);
        }

        private static bool IsPublicRoute(string path)
        {
            return PublicRoutes.Any(route => path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
        }
    }
}

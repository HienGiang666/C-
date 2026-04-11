namespace TourApp.CMS.Services
{
    public class ActivityLog
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Entity { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string IpAddress { get; set; } = string.Empty;
    }

    public interface IActivityLogger
    {
        void LogActivity(HttpContext context, string action, string entity, string? oldValue = null, string? newValue = null);
        (IReadOnlyList<ActivityLog> Items, int TotalCount) GetLogsPaged(HttpContext? httpContext, int pageIndex0, int pageSize, string? usernameFilter);
        void ClearVisibleHistory(HttpContext httpContext);
    }

    public class ActivityLogger : IActivityLogger
    {
        private static readonly List<ActivityLog> _logs = new();
        private static int _nextLogId = 1;
        private static readonly object _logLock = new();

        private const string HiddenBeforeSessionKey = "ActivityLogHiddenBefore";

        private readonly ILogger<ActivityLogger> _logger;

        public ActivityLogger(ILogger<ActivityLogger> logger)
        {
            _logger = logger;
        }

        public void LogActivity(HttpContext context, string action, string entity, string? oldValue = null, string? newValue = null)
        {
            try
            {
                var username = context.Session.GetString("Username") ?? "Anonymous";
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

                lock (_logLock)
                {
                    var log = new ActivityLog
                    {
                        Id = _nextLogId++,
                        Username = username,
                        Action = action,
                        Entity = entity,
                        OldValue = oldValue,
                        NewValue = newValue,
                        Timestamp = DateTime.Now,
                        IpAddress = ipAddress
                    };
                    _logs.Add(log);
                    while (_logs.Count > 1000)
                        _logs.RemoveAt(0);
                }

                _logger.LogInformation("Activity logged: {Username} - {Action} - {Entity}", username, action, entity);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error logging activity: {Message}", ex.Message);
            }
        }

        public void ClearVisibleHistory(HttpContext httpContext)
        {
            httpContext.Session.SetString(HiddenBeforeSessionKey, DateTime.Now.ToString("o"));
        }

        private static DateTime? ReadHiddenBefore(HttpContext? httpContext)
        {
            if (httpContext?.Session == null)
                return null;
            var raw = httpContext.Session.GetString(HiddenBeforeSessionKey);
            return DateTime.TryParse(raw, out var dt) ? dt : null;
        }

        public (IReadOnlyList<ActivityLog> Items, int TotalCount) GetLogsPaged(HttpContext? httpContext, int pageIndex0, int pageSize, string? usernameFilter)
        {
            pageIndex0 = Math.Max(0, pageIndex0);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var hiddenBefore = ReadHiddenBefore(httpContext);
            List<ActivityLog> ordered;
            lock (_logLock)
            {
                IEnumerable<ActivityLog> q = _logs;
                if (hiddenBefore.HasValue)
                    q = q.Where(l => l.Timestamp > hiddenBefore.Value);
                if (!string.IsNullOrWhiteSpace(usernameFilter))
                    q = q.Where(l => l.Username.Equals(usernameFilter, StringComparison.OrdinalIgnoreCase));
                ordered = q.OrderBy(l => l.Timestamp).ToList();
            }

            var total = ordered.Count;
            var page = ordered.Skip(pageIndex0 * pageSize).Take(pageSize).ToList();
            return (page, total);
        }
    }
}

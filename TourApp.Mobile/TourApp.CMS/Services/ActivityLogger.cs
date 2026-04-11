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
        List<ActivityLog> GetLogs(int limit = 100);
        List<ActivityLog> GetLogsForUser(string username, int limit = 200);
    }

    public class ActivityLogger : IActivityLogger
    {
        private static List<ActivityLog> _logs = new List<ActivityLog>();
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

                var log = new ActivityLog
                {
                    Id = _logs.Count + 1,
                    Username = username,
                    Action = action,
                    Entity = entity,
                    OldValue = oldValue,
                    NewValue = newValue,
                    Timestamp = DateTime.Now,
                    IpAddress = ipAddress
                };

                _logs.Insert(0, log); // Thêm vào đầu danh sách

                // Giữ tối đa 1000 logs
                if (_logs.Count > 1000)
                    _logs = _logs.Take(1000).ToList();

                _logger.LogInformation($"Activity logged: {username} - {action} - {entity}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error logging activity: {ex.Message}");
            }
        }

        public List<ActivityLog> GetLogs(int limit = 100)
        {
            return _logs.Take(limit).ToList();
        }

        public List<ActivityLog> GetLogsForUser(string username, int limit = 200)
        {
            if (string.IsNullOrWhiteSpace(username))
                return new List<ActivityLog>();
            return _logs
                .Where(l => l.Username.Equals(username, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();
        }
    }
}

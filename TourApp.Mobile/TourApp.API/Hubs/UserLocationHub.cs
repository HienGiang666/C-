using Microsoft.AspNetCore.SignalR;

namespace TourApp.API.Hubs;

/// <summary>
/// SignalR Hub cho real-time user location tracking
/// </summary>
public class UserLocationHub : Hub
{
    private readonly ILogger<UserLocationHub> _logger;

    public UserLocationHub(ILogger<UserLocationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Client gọi khi user mở app / bắt đầu session
    /// </summary>
    public async Task UserOnline(string deviceId, string sessionId, double latitude, double longitude)
    {
        _logger.LogInformation("[SignalR] User {DeviceId} online at ({Lat}, {Lng})", deviceId, latitude, longitude);
        
        // Track connection để khi disconnect biết được device nào offline
        UserLocationTracker.AddConnection(Context.ConnectionId, deviceId, sessionId);
        
        // Broadcast tới tất cả CMS clients về user mới online
        await Clients.All.SendAsync("UserOnline", new
        {
            DeviceId = deviceId,
            SessionId = sessionId,
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Client gọi khi user cập nhật vị trí (di chuyển)
    /// </summary>
    public async Task UpdateLocation(string deviceId, string sessionId, double latitude, double longitude)
    {
        _logger.LogDebug("[SignalR] User {DeviceId} moved to ({Lat}, {Lng})", deviceId, latitude, longitude);
        
        // Broadcast vị trí mới tới CMS
        await Clients.All.SendAsync("LocationUpdated", new
        {
            DeviceId = deviceId,
            SessionId = sessionId,
            Latitude = latitude,
            Longitude = longitude,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// Client gọi khi user tắt app / offline
    /// </summary>
    public async Task UserOffline(string deviceId, string sessionId)
    {
        _logger.LogInformation("[SignalR] User {DeviceId} offline", deviceId);
        
        // Broadcast user offline tới CMS
        await Clients.All.SendAsync("UserOffline", new
        {
            DeviceId = deviceId,
            SessionId = sessionId,
            Timestamp = DateTime.Now
        });
    }

    /// <summary>
    /// CMS Dashboard gọi để lấy danh sách user đang online
    /// </summary>
    public async Task GetOnlineUsers()
    {
        // Trả về số lượng connections hiện tại (approximate online users)
        var connectionCount = UserLocationTracker.GetOnlineCount();
        
        await Clients.Caller.SendAsync("OnlineUsersCount", new
        {
            Count = connectionCount,
            Timestamp = DateTime.Now
        });
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[SignalR] Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[SignalR] Client disconnected: {ConnectionId}", Context.ConnectionId);
        
        // Nếu là mobile app disconnect, broadcast offline
        var deviceId = UserLocationTracker.GetDeviceIdByConnection(Context.ConnectionId);
        if (!string.IsNullOrEmpty(deviceId))
        {
            await Clients.All.SendAsync("UserOffline", new
            {
                DeviceId = deviceId,
                SessionId = UserLocationTracker.GetSessionId(Context.ConnectionId),
                Timestamp = DateTime.Now
            });
            UserLocationTracker.RemoveConnection(Context.ConnectionId);
        }
        
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Static tracker để theo dõi connection <-> device mapping
/// </summary>
public static class UserLocationTracker
{
    private static readonly Dictionary<string, (string DeviceId, string SessionId)> _connections = new();

    public static void AddConnection(string connectionId, string deviceId, string sessionId)
    {
        lock (_connections)
        {
            _connections[connectionId] = (deviceId, sessionId);
        }
    }

    public static void RemoveConnection(string connectionId)
    {
        lock (_connections)
        {
            _connections.Remove(connectionId);
        }
    }

    public static string? GetDeviceIdByConnection(string connectionId)
    {
        lock (_connections)
        {
            return _connections.TryGetValue(connectionId, out var info) ? info.DeviceId : null;
        }
    }

    public static string? GetSessionId(string connectionId)
    {
        lock (_connections)
        {
            return _connections.TryGetValue(connectionId, out var info) ? info.SessionId : null;
        }
    }

    public static int GetOnlineCount()
    {
        lock (_connections)
        {
            return _connections.Count;
        }
    }
}

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using TourApp.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserLocationController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<UserLocationHub> _hubContext;

    public UserLocationController(AppDbContext context, IHubContext<UserLocationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserLocationLog>>> GetLogs()
    {
        return await _context.UserLocationLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<UserLocationLog>> LogLocation(UserLocationLog log)
    {
        log.Timestamp = DateTime.Now;
        log.IsActive = true; // Đánh dấu đang online
        
        _context.UserLocationLogs.Add(log);
        await _context.SaveChangesAsync();
        
        // Broadcast real-time location update qua SignalR
        await _hubContext.Clients.All.SendAsync("LocationUpdated", new
        {
            log.DeviceId,
            log.SessionId,
            log.Latitude,
            log.Longitude,
            log.Timestamp,
            log.IsActive
        });
        
        return Ok(log);
    }

    /// <summary>
    /// Session endpoint: mobile app gọi khi user online/offline
    /// </summary>
    [HttpPost("session")]
    public async Task<IActionResult> SessionUpdate([FromBody] UserSessionRequest request)
    {
        var deviceId = request.GuestId ?? $"user_{request.UserId}" ?? "unknown";
        
        if (request.IsOnline)
        {
            await _hubContext.Clients.All.SendAsync("UserOnline", new
            {
                DeviceId = deviceId,
                SessionId = request.GuestId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Timestamp = DateTime.Now
            });
        }
        else
        {
            await _hubContext.Clients.All.SendAsync("UserOffline", new
            {
                DeviceId = deviceId,
                SessionId = request.GuestId,
                Timestamp = DateTime.Now
            });
        }
        
        return Ok(new { success = true });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var oneMinuteAgo = DateTime.Now.AddMinutes(-1);
        var since24h = DateTime.Now.AddHours(-24);
        
        // Lấy tất cả logs trong 24h, group by DeviceId, lấy log mới nhất mỗi device
        var recentLogs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
        
        var latestByDevice = recentLogs
            .GroupBy(l => l.DeviceId)
            .Select(g => g.First()) // Log mới nhất của mỗi device
            .ToList();
        
        // Online = log mới nhất trong vòng 1 phút
        var onlineDevices = latestByDevice
            .Where(l => l.Timestamp >= oneMinuteAgo)
            .ToList();
        
        var onlineNow = onlineDevices.Count;
        var active24h = latestByDevice.Count;
        
        // Vị trí online (dùng log mới nhất trong 1 phút)
        var onlineLocations = onlineDevices.Select(l => new
        {
            l.DeviceId,
            l.Latitude,
            l.Longitude,
            l.Timestamp,
            l.SessionId
        }).ToList();

        return Ok(new
        {
            onlineNow,
            active24h,
            onlineLocations
        });
    }

    /// <summary>
    /// Heatmap: trả về tất cả điểm location trong 24h cho Leaflet.heat
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmap()
    {
        var since24h = DateTime.Now.AddHours(-24);
        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .Select(l => new[] { l.Latitude, l.Longitude, 0.5 })
            .ToListAsync();

        return Ok(new { points = logs });
    }

    /// <summary>
    /// Paths: trả về tuyến đường phổ biến (POI → POI) dựa trên dữ liệu location
    /// </summary>
    [HttpGet("paths")]
    public async Task<IActionResult> GetPaths()
    {
        var since24h = DateTime.Now.AddHours(-24);
        
        // Lấy logs theo device, sắp xếp theo thời gian
        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderBy(l => l.DeviceId).ThenBy(l => l.Timestamp)
            .ToListAsync();

        // Tạo tuyến đường từ mỗi device (nối điểm đầu → cuối)
        var routes = logs
            .GroupBy(l => l.DeviceId)
            .Where(g => g.Count() >= 2)
            .Select(g =>
            {
                var pts = g.ToList();
                return new
                {
                    fromLat = pts.First().Latitude,
                    fromLng = pts.First().Longitude,
                    toLat = pts.Last().Latitude,
                    toLng = pts.Last().Longitude,
                    fromPoiName = pts.First().DeviceId ?? "Start",
                    toPoiName = pts.Last().DeviceId ?? "End",
                    count = pts.Count
                };
            })
            .ToList();

        return Ok(new { routes });
    }

    /// <summary>
    /// History: lịch sử hoạt động 24h - danh sách user + thống kê
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var since24h = DateTime.Now.AddHours(-24);
        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        var byDevice = logs.GroupBy(l => l.DeviceId).ToList();
        
        var users = byDevice.Select(g => new
        {
            deviceId = g.Key ?? "unknown",
            isAnonymous = g.Key == null || !g.Key.StartsWith("user_"),
            lastSeen = g.First().Timestamp,
            locationCount = g.Count(),
            lastLocation = new
            {
                lat = g.First().Latitude,
                lng = g.First().Longitude
            }
        }).ToList();

        return Ok(new
        {
            totalUsers = byDevice.Count,
            anonymousUsers = users.Count(u => u.isAnonymous),
            users
        });
    }
}

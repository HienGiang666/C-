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
            // Lưu location log khi online (heartbeat) — luôn lưu dù GPS tắt
            _context.UserLocationLogs.Add(new UserLocationLog
            {
                DeviceId = deviceId,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                Timestamp = DateTime.Now,
                SessionId = request.GuestId,
                IsActive = true
            });
            await _context.SaveChangesAsync();

            await _hubContext.Clients.All.SendAsync("UserOnline", new
            {
                DeviceId = deviceId,
                SessionId = request.GuestId,
                Name = request.Name,
                DeviceInfo = request.DeviceInfo,
                Platform = request.Platform,
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

    /// <summary>
    /// Management stats: dữ liệu chi tiết cho trang Quản lý người dùng
    /// </summary>
    [HttpGet("management-stats")]
    public async Task<IActionResult> GetManagementStats()
    {
        var since24h = DateTime.Now.AddHours(-24);
        var oneMinuteAgo = DateTime.Now.AddMinutes(-1);

        // Tổng thiết bị đã vào app (all time)
        var totalDevices = await _context.UserLocationLogs
            .Select(l => l.DeviceId)
            .Distinct()
            .CountAsync();

        // Số lượt quét QR (trigger = "qr")
        var qrScanCount = await _context.NarrationLogs
            .Where(l => l.TriggerType == "qr")
            .CountAsync();

        // Logs 24h
        var recentLogs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();

        var byDevice = recentLogs.GroupBy(l => l.DeviceId).ToList();

        // Chi tiết từng user
        var users = byDevice.Select(g =>
        {
            var logs = g.OrderByDescending(l => l.Timestamp).ToList();
            var firstLog = logs.Last();
            var lastLog = logs.First();
            var isOnline = lastLog.Timestamp >= oneMinuteAgo;

            return new
            {
                deviceId = g.Key ?? "unknown",
                isAnonymous = g.Key == null || !g.Key.StartsWith("user_"),
                isOnline,
                firstSeen = firstLog.Timestamp,
                lastSeen = lastLog.Timestamp,
                locationCount = logs.Count,
                lastLocation = new { lat = lastLog.Latitude, lng = lastLog.Longitude }
            };
        }).OrderByDescending(u => u.isOnline).ThenByDescending(u => u.lastSeen).ToList();

        // Lấy narration logs gần đây để biết ngôn ngữ dùng
        var narrationLogs = await _context.NarrationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderByDescending(l => l.Timestamp)
            .Take(200)
            .ToListAsync();

        // Lấy audio language info
        var audioIds = narrationLogs.Where(n => n.AudioId.HasValue).Select(n => n.AudioId!.Value).Distinct().ToList();
        var audioLanguages = await _context.Audios
            .Where(a => audioIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Language })
            .ToDictionaryAsync(a => a.Id, a => a.Language ?? "vi");

        var narrationByDevice = narrationLogs.GroupBy(n => n.DeviceId).ToDictionary(
            g => g.Key ?? "unknown",
            g => new
            {
                playCount = g.Count(),
                languages = g.Where(n => n.AudioId.HasValue && audioLanguages.ContainsKey(n.AudioId!.Value))
                    .Select(n => audioLanguages[n.AudioId!.Value])
                    .Distinct().ToList(),
                qrCount = g.Count(n => n.TriggerType == "qr")
            });

        return Ok(new
        {
            totalDevices,
            qrScanCount,
            onlineNow = users.Count(u => u.isOnline),
            active24h = users.Count,
            users,
            narrationByDevice
        });
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
    /// Paths: trả về các tuyến di chuyển phổ biến (polyline đầy đủ điểm GPS, bỏ noise)
    /// </summary>
    [HttpGet("paths")]
    public async Task<IActionResult> GetPaths()
    {
        var since24h = DateTime.Now.AddHours(-24);

        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderBy(l => l.DeviceId).ThenBy(l => l.Timestamp)
            .Select(l => new { l.DeviceId, l.Latitude, l.Longitude, l.Timestamp })
            .ToListAsync();

        var routes = new List<(string deviceId, List<double[]> coords, double totalMeters)>();

        foreach (var g in logs.GroupBy(l => l.DeviceId))
        {
            var pts = g.ToList();
            if (pts.Count < 3) continue;

            // Simplify: chỉ giữ điểm cách điểm trước > 100m (loại noise đứng yên)
            var simplified = new List<double[]>();
            double lastLat = pts[0].Latitude, lastLng = pts[0].Longitude;
            double totalDist = 0;
            simplified.Add(new[] { lastLng, lastLat });

            for (int i = 1; i < pts.Count; i++)
            {
                var dist = HaversineMeters(lastLat, lastLng, pts[i].Latitude, pts[i].Longitude);
                if (dist > 100)
                {
                    simplified.Add(new[] { pts[i].Longitude, pts[i].Latitude });
                    totalDist += dist;
                    lastLat = pts[i].Latitude;
                    lastLng = pts[i].Longitude;
                }
            }

            if (simplified.Count >= 3 && totalDist > 500)
            {
                routes.Add((g.Key ?? "anon", simplified, totalDist));
            }
        }

        // Chỉ lấy các tuyến phổ biến (nhiều device có đường gần giống nhau)
        // Làm tròn start và end để gộp các tuyến tương tự
        var grouped = routes
            .Select(r => new
            {
                r.deviceId,
                r.coords,
                r.totalMeters,
                StartLat = Math.Round(r.coords.First()[1], 2),
                StartLng = Math.Round(r.coords.First()[0], 2),
                EndLat = Math.Round(r.coords.Last()[1], 2),
                EndLng = Math.Round(r.coords.Last()[0], 2)
            })
            .GroupBy(x => new { x.StartLat, x.StartLng, x.EndLat, x.EndLng })
            .Select(g => new
            {
                coordinates = g.OrderByDescending(x => x.totalMeters).First().coords,
                count = g.Count(),
                devices = g.Select(x => x.deviceId).Distinct().Count(),
                totalMeters = g.Max(x => x.totalMeters)
            })
            .Where(x => x.count >= 2 || x.devices >= 2)
            .OrderByDescending(x => x.totalMeters)
            .Take(100)
            .ToList();

        return Ok(new { routes = grouped });
    }

    private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return R * c;
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

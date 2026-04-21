using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using TourApp.API.Hubs;
using Microsoft.AspNetCore.SignalR;

// API Routes and User Movement Analytics Controller
// Uses existing UserLocationLogs and POIs tables - NO schema changes

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
    /// GET /api/userlocation/stats
    /// Trả về thống kê người dùng đang online (theo Timestamp gần nhất trong 1 phút)
    /// và tổng số device unique trong 24h qua
    /// </summary>
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
    /// GET /api/userlocation/heatmap
    /// Trả về tất cả vị trí trong 24h qua để vẽ heatmap mật độ
    /// </summary>
    [HttpGet("heatmap")]
    public async Task<IActionResult> GetHeatmapData()
    {
        var since24h = DateTime.Now.AddHours(-24);
        
        // Lấy tất cả location logs trong 24h
        var locations = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .Select(l => new
            {
                l.Latitude,
                l.Longitude,
                l.Timestamp,
                l.DeviceId
            })
            .ToListAsync();
        
        // Format cho heatmap: [lat, lng, intensity]
        var heatmapPoints = locations.Select(l => new[] { l.Latitude, l.Longitude, 0.8 });
        
        return Ok(new
        {
            totalPoints = locations.Count,
            points = heatmapPoints
        });
    }

    // CONST: Bán kính tìm POI gần nhất (meters)
    private const double POI_RADIUS_METERS = 100;

    /// <summary>
    /// GET /api/userlocation/paths
    /// Trả về TUYẾN DI CHUYỂN PHỔ BIẾN giữa các POI (popular routes)
    /// Không đổi DB - dùng UserLocationLogs + POIs hiện có
    /// </summary>
    [HttpGet("paths")]
    public async Task<IActionResult> GetMovementPaths()
    {
        var since24h = DateTime.Now.AddHours(-24);
        
        // 1. Lấy tất cả POIs (không đổi DB)
        var pois = await _context.POIs
            .Select(p => new { p.Id, p.Name, p.Latitude, p.Longitude })
            .ToListAsync();
        
        // 2. Lấy location logs 24h qua, group by device
        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderBy(l => l.DeviceId)
            .ThenBy(l => l.Timestamp)
            .Select(l => new { l.DeviceId, l.Latitude, l.Longitude, l.Timestamp })
            .ToListAsync();
        
        var devicePaths = logs
            .GroupBy(l => l.DeviceId)
            .Where(g => g.Count() >= 2) // Chỉ device có >= 2 điểm
            .ToList();
        
        // 3. Với mỗi device, map locations thành chuỗi POI
        var allTransitions = new List<(int fromId, int toId)>();
        
        foreach (var deviceLog in devicePaths)
        {
            var poisVisited = new List<int>();
            
            foreach (var loc in deviceLog)
            {
                // Tìm POI gần nhất trong bán kính POI_RADIUS_METERS
                var nearestPoi = pois
                    .Select(p => new { p, dist = CalculateHaversineDistance(loc.Latitude, loc.Longitude, p.Latitude, p.Longitude) })
                    .Where(x => x.dist <= POI_RADIUS_METERS)
                    .OrderBy(x => x.dist)
                    .FirstOrDefault();
                
                if (nearestPoi != null)
                {
                    poisVisited.Add(nearestPoi.p.Id);
                }
            }
            
            // Bỏ POI trùng liên tiếp
            var uniquePois = new List<int>();
            int? lastPoi = null;
            foreach (var poiId in poisVisited)
            {
                if (poiId != lastPoi)
                {
                    uniquePois.Add(poiId);
                    lastPoi = poiId;
                }
            }
            
            // Tạo các cặp chuyển tiếp A -> B
            for (int i = 0; i < uniquePois.Count - 1; i++)
            {
                allTransitions.Add((uniquePois[i], uniquePois[i + 1]));
            }
        }
        
        // 4. Group các cặp để đếm số lần xuất hiện
        var popularRoutes = allTransitions
            .GroupBy(t => t)
            .Select(g => new { FromId = g.Key.fromId, ToId = g.Key.toId, Count = g.Count() })
            .Where(r => r.FromId != r.ToId && r.Count > 0) // Loại route A->A
            .OrderByDescending(r => r.Count)
            .Take(20) // Top 20 routes phổ biến
            .ToList();
        
        // 5. Map sang response với POI names và coords
        var routes = popularRoutes
            .Select(r => {
                var fromPoi = pois.First(p => p.Id == r.FromId);
                var toPoi = pois.First(p => p.Id == r.ToId);
                return new
                {
                    fromPoiId = r.FromId,
                    fromPoiName = fromPoi.Name,
                    fromLat = fromPoi.Latitude,
                    fromLng = fromPoi.Longitude,
                    toPoiId = r.ToId,
                    toPoiName = toPoi.Name,
                    toLat = toPoi.Latitude,
                    toLng = toPoi.Longitude,
                    count = r.Count
                };
            })
            .ToList();
        
        return Ok(new
        {
            totalRoutes = routes.Count,
            poiRadiusMeters = POI_RADIUS_METERS,
            routes
        });
    }
    
    /// <summary>
    /// Tính khoảng cách Haversine giữa 2 điểm (meters)
    /// </summary>
    private double CalculateHaversineDistance(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000; // Earth radius in meters
        
        var dLat = ToRadians(lat2 - lat1);
        var dLng = ToRadians(lng2 - lng1);
        
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) *
                Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        
        return R * c;
    }
    
    private double ToRadians(double degrees) => degrees * Math.PI / 180;

    /// <summary>
    /// GET /api/userlocation/history
    /// Trả về lịch sử hoạt động của tất cả users (có tài khoản + ẩn danh)
    /// </summary>
    [HttpGet("history")]
    public async Task<IActionResult> GetUserHistory()
    {
        var since24h = DateTime.Now.AddHours(-24);
        
        // Lấy tất cả logs trong 24h, group by device, lấy thông tin mới nhất
        var logs = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .OrderByDescending(l => l.Timestamp)
            .ToListAsync();
        
        var latestByDevice = logs
            .GroupBy(l => l.DeviceId)
            .Select(g => new
            {
                deviceId = g.Key,
                sessionId = g.First().SessionId,
                lastSeen = g.First().Timestamp,
                firstSeen = g.Last().Timestamp,
                locationCount = g.Count(),
                lastLocation = new
                {
                    lat = g.First().Latitude,
                    lng = g.First().Longitude
                },
                isAnonymous = string.IsNullOrEmpty(g.First().SessionId) || g.First().SessionId.StartsWith("anon_")
            })
            .OrderByDescending(u => u.lastSeen)
            .Take(50)
            .ToList();
        
        return Ok(new
        {
            totalUsers = latestByDevice.Count,
            anonymousUsers = latestByDevice.Count(u => u.isAnonymous),
            registeredUsers = latestByDevice.Count(u => !u.isAnonymous),
            users = latestByDevice
        });
    }
}

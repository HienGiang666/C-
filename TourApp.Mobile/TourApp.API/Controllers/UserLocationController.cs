using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using TourApp.API.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace TourApp.API.Controllers;

public class UserSessionRequest
{
    public int? UserId { get; set; }
    public string? GuestId { get; set; }
    public bool IsOnline { get; set; }
    public string? Name { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Platform { get; set; }
    public string? Version { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

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
    /// Mobile app gọi khi mở app / heartbeat để giữ trạng thái online
    /// </summary>
    [HttpPost("session")]
    public async Task<ActionResult<UserLocationLog>> UpdateSession([FromBody] UserSessionRequest request)
    {
        if (request == null)
        {
            return BadRequest("Request body is required.");
        }

        var deviceId = !string.IsNullOrWhiteSpace(request.GuestId)
            ? request.GuestId
            : request.UserId?.ToString();

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return BadRequest("UserId or GuestId is required.");
        }

        var log = new UserLocationLog
        {
            DeviceId = deviceId,
            SessionId = request.GuestId ?? request.UserId?.ToString(),
            Latitude = request.Latitude ?? 0,
            Longitude = request.Longitude ?? 0,
            Timestamp = DateTime.Now,
            IsActive = request.IsOnline
        };

        _context.UserLocationLogs.Add(log);
        await _context.SaveChangesAsync();

        await _hubContext.Clients.All.SendAsync(request.IsOnline ? "UserOnline" : "UserOffline", new
        {
            log.DeviceId,
            log.SessionId,
            log.Timestamp,
            log.IsActive,
            request.Name,
            request.DeviceInfo,
            request.Platform,
            request.Version
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
        
        // Online = log mới nhất trong vòng 1 phút, trạng thái active, và có tọa độ hợp lệ
        var onlineDevices = latestByDevice
            .Where(l => l.Timestamp >= oneMinuteAgo
                        && l.IsActive
                        && !(l.Latitude == 0 && l.Longitude == 0))
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
}

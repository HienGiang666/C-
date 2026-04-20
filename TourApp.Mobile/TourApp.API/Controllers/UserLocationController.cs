using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserLocationController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserLocationController(AppDbContext context)
    {
        _context = context;
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
        _context.UserLocationLogs.Add(log);
        await _context.SaveChangesAsync();
        return Ok(log);
    }

    /// <summary>
    /// GET /api/userlocation/stats
    /// Trả về thống kê người dùng đang online (IsActive = true)
    /// và tổng số device unique trong 24h qua
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        // Số người đang online (IsActive = true)
        var onlineNow = await _context.UserLocationLogs
            .Where(l => l.IsActive)
            .Select(l => l.DeviceId)
            .Distinct()
            .CountAsync();

        // Số device unique trong 24h qua
        var since24h = DateTime.Now.AddHours(-24);
        var active24h = await _context.UserLocationLogs
            .Where(l => l.Timestamp >= since24h)
            .Select(l => l.DeviceId)
            .Distinct()
            .CountAsync();

        // Lấy danh sách device đang online với vị trí mới nhất
        var onlineDeviceIds = await _context.UserLocationLogs
            .Where(l => l.IsActive)
            .Select(l => l.DeviceId)
            .Distinct()
            .ToListAsync();

        // Lấy vị trí mới nhất của từng device đang online
        var onlineLocations = new List<object>();
        foreach (var deviceId in onlineDeviceIds)
        {
            var latestLocation = await _context.UserLocationLogs
                .Where(l => l.DeviceId == deviceId && l.IsActive)
                .OrderByDescending(l => l.Timestamp)
                .FirstOrDefaultAsync();

            if (latestLocation != null)
            {
                onlineLocations.Add(new
                {
                    latestLocation.DeviceId,
                    latestLocation.Latitude,
                    latestLocation.Longitude,
                    latestLocation.Timestamp,
                    latestLocation.SessionId
                });
            }
        }

        return Ok(new
        {
            onlineNow,
            active24h,
            onlineLocations
        });
    }
}

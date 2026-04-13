using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class NarrationLogController : ControllerBase
{
    private readonly AppDbContext _context;

    public NarrationLogController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NarrationLog>>> GetLogs([FromQuery] int? poiId, [FromQuery] int top = 100)
    {
        var q = _context.NarrationLogs.AsQueryable();
        if (poiId.HasValue) q = q.Where(l => l.POIId == poiId.Value);
        return await q.OrderByDescending(l => l.Timestamp).Take(top).ToListAsync();
    }

    [HttpPost]
    public async Task<IActionResult> LogNarration(NarrationLog log)
    {
        log.Timestamp = DateTime.Now;
        _context.NarrationLogs.Add(log);
        await _context.SaveChangesAsync();
        return Ok(new { id = log.Id });
    }

    /// <summary>
    /// GET /api/narrationlog/stats
    /// Trả về số liệu phân tích cho CMS Dashboard:
    /// - Top POI được nghe nhiều nhất (top 5)
    /// - Phân loại trigger: geofence vs qr
    /// - Tổng lượt nghe theo từng ngày (7 ngày gần nhất)
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        // Top 5 POI by play count
        var topPoi = await _context.NarrationLogs
            .GroupBy(l => l.POIId)
            .Select(g => new { PoiId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync();

        // Lấy tên POI
        var poiIds = topPoi.Select(x => x.PoiId).ToList();
        var poiNames = await _context.POIs
            .Where(p => poiIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name);

        var topPoiResult = topPoi.Select(x => new
        {
            x.PoiId,
            PoiName = poiNames.TryGetValue(x.PoiId, out var n) ? n : $"POI #{x.PoiId}",
            x.Count
        }).ToList();

        // Trigger type breakdown
        var triggerStats = await _context.NarrationLogs
            .GroupBy(l => l.TriggerType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        // Daily plays (7 ngày gần nhất)
        var since = DateTime.Now.AddDays(-7).Date;
        var dailyPlays = await _context.NarrationLogs
            .Where(l => l.Timestamp >= since)
            .GroupBy(l => l.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync();

        // Total
        var total = await _context.NarrationLogs.CountAsync();

        return Ok(new
        {
            total,
            topPoi = topPoiResult,
            triggerStats,
            dailyPlays = dailyPlays.Select(d => new
            {
                date = d.Date.ToString("dd/MM"),
                d.Count
            })
        });
    }
}

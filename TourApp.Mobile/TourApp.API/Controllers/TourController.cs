using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TourController : ControllerBase
{
    private readonly AppDbContext _context;

    public TourController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Tour>>> GetTours()
    {
        return await _context.Tours.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Tour>> GetTour(int id)
    {
        var tour = await _context.Tours.FindAsync(id);
        if (tour == null) return NotFound();
        return tour;
    }

    /// <summary>Danh sách điểm ghé theo thứ tự (OrderIndex) — dùng cho mobile/CMS.</summary>
    [HttpGet("{id}/stops")]
    public async Task<IActionResult> GetTourStops(int id)
    {
        if (!await _context.Tours.AnyAsync(t => t.Id == id))
            return NotFound();
        var stops = await _context.TourPOIs
            .Include(x => x.POI)
            .Where(x => x.TourId == id)
            .OrderBy(x => x.OrderIndex)
            .Select(x => new
            {
                id = x.Id,
                tourId = x.TourId,
                poiId = x.POIId,
                orderIndex = x.OrderIndex,
                poi = x.POI == null ? null : new
                {
                    id = x.POI.Id,
                    name = x.POI.Name,
                    latitude = x.POI.Latitude,
                    longitude = x.POI.Longitude,
                    imageUrl = x.POI.ImageUrl
                }
            })
            .ToListAsync();
        return Ok(stops);
    }

    /// <summary>Thay toàn bộ điểm dừng của tour theo thứ tự mảng POI Id.</summary>
    [HttpPut("{id}/stops")]
    public async Task<IActionResult> ReplaceTourStops(int id, [FromBody] int[]? poiIdsInOrder)
    {
        if (!await _context.Tours.AnyAsync(t => t.Id == id))
            return NotFound();
        var existing = _context.TourPOIs.Where(x => x.TourId == id);
        _context.TourPOIs.RemoveRange(existing);
        if (poiIdsInOrder != null && poiIdsInOrder.Length > 0)
        {
            var order = 1;
            foreach (var poiId in poiIdsInOrder)
            {
                if (!await _context.POIs.AnyAsync(p => p.Id == poiId))
                    continue;
                _context.TourPOIs.Add(new TourPOI { TourId = id, POIId = poiId, OrderIndex = order++ });
            }
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<Tour>> CreateTour(Tour tour)
    {
        // Auto-generate Code nếu chưa có
        if (string.IsNullOrEmpty(tour.Code))
        {
            var maxCodeNum = await _context.Tours
                .Select(t => t.Code)
                .ToListAsync();
            var nextNum = maxCodeNum
                .Where(c => !string.IsNullOrEmpty(c) && c!.StartsWith("TR-"))
                .Select(c => int.TryParse(c!.Substring(3), out var n) ? n : 0)
                .DefaultIfEmpty(1000)
                .Max() + 1;
            tour.Code = $"TR-{nextNum}";
        }
        _context.Tours.Add(tour);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTour), new { id = tour.Id }, tour);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTour(int id, Tour tour)
    {
        if (id != tour.Id) return BadRequest();
        var existing = await _context.Tours.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (existing != null)
            tour.Code = existing.Code; // Giữ nguyên Code cũ
        _context.Entry(tour).State = EntityState.Modified;
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Tours.Any(e => e.Id == id))
                return NotFound();
            else
                throw;
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTour(int id)
    {
        var tour = await _context.Tours.FindAsync(id);
        if (tour == null) return NotFound();

        var bookings = _context.Bookings.Where(b => b.TourId == id);
        _context.Bookings.RemoveRange(bookings);
        var stops = _context.TourPOIs.Where(x => x.TourId == id);
        _context.TourPOIs.RemoveRange(stops);
        _context.Tours.Remove(tour);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

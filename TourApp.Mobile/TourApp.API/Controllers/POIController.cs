using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class POIController : ControllerBase
{
    private readonly AppDbContext _context;

    public POIController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<POI>>> GetPOIs()
    {
        return await _context.POIs.ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<POI>> CreatePOI(POI poi)
    {
        _context.POIs.Add(poi);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPOIs), new { id = poi.Id }, poi);
    }
    // VÒI 3: SỬA thông tin địa điểm (PUT)
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePOI(int id, POI poi)
    {
        if (id != poi.Id)
        {
            return BadRequest("ID không khớp!");
        }

        _context.Entry(poi).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.POIs.Any(e => e.Id == id))
                return NotFound("Không tìm thấy địa điểm để sửa!");
            else
                throw;
        }

        return NoContent(); 
    }
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePOI(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null)
        {
            return NotFound("Không tìm thấy địa điểm để xóa!");
        }

        _context.POIs.Remove(poi);
        await _context.SaveChangesAsync();

        return NoContent(); 
    }
}
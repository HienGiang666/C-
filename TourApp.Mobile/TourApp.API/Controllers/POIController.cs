using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using TourApp.API.Services;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class POIController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly BusinessKeyService _keyService;

    public POIController(AppDbContext context, BusinessKeyService keyService)
    {
        _context = context;
        _keyService = keyService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<POI>>> GetPOIs([FromQuery] int? ownerUserId, [FromQuery] bool approvedOnly = false)
    {
        var q = _context.POIs.Include(p => p.Audios).Include(p => p.Translations).AsSplitQuery().AsQueryable();
        if (approvedOnly)
            q = q.Where(p => p.ApprovalStatus == "Approved" && p.IsActive);
        if (ownerUserId.HasValue)
            q = q.Where(p => p.OwnerUserId == ownerUserId.Value);
        return await q.OrderBy(p => p.Code).ThenBy(p => p.Id).ToListAsync();
    }

    [HttpGet("pending")]
    public async Task<ActionResult<IEnumerable<POI>>> GetPendingPOIs()
    {
        return await _context.POIs.Include(p => p.Audios).Include(p => p.Translations).AsSplitQuery()
            .Where(p => p.ApprovalStatus == "Pending")
            .OrderByDescending(p => p.Id) // Sắp xếp cái mới nhất (ID lớn nhất) lên đầu
            .ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<POI>> GetPOI(int id)
    {
        var poi = await _context.POIs.Include(p => p.Audios).Include(p => p.Translations).AsSplitQuery().FirstOrDefaultAsync(p => p.Id == id);

        if (poi == null)
        {
            return NotFound("Không tìm thấy địa điểm!");
        }

        return poi;
    }

    // ===== TRANSLATION ENDPOINTS =====

    [HttpGet("{poiId}/translations")]
    public async Task<ActionResult<IEnumerable<POITranslation>>> GetTranslations(int poiId)
    {
        return await _context.POITranslations.Where(t => t.POIId == poiId).ToListAsync();
    }

    [HttpGet("{poiId}/translations/{lang}")]
    public async Task<ActionResult<POITranslation>> GetTranslation(int poiId, string lang)
    {
        var t = await _context.POITranslations.FirstOrDefaultAsync(x => x.POIId == poiId && x.Language == lang);
        if (t == null) return NotFound();
        return t;
    }

    [HttpPost("{poiId}/translations")]
    public async Task<ActionResult<POITranslation>> SaveTranslation(int poiId, POITranslation translation)
    {
        translation.POIId = poiId;
        var existing = await _context.POITranslations
            .FirstOrDefaultAsync(x => x.POIId == poiId && x.Language == translation.Language);

        if (existing != null)
        {
            existing.Name = translation.Name;
            existing.Description = translation.Description;
        }
        else
        {
            _context.POITranslations.Add(translation);
        }
        await _context.SaveChangesAsync();
        return Ok(translation);
    }

    [HttpDelete("{poiId}/translations/{lang}")]
    public async Task<IActionResult> DeleteTranslation(int poiId, string lang)
    {
        var t = await _context.POITranslations.FirstOrDefaultAsync(x => x.POIId == poiId && x.Language == lang);
        if (t == null) return NotFound();
        _context.POITranslations.Remove(t);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===== END TRANSLATION ENDPOINTS =====

    [HttpPost("{id}/approve")]
    public async Task<IActionResult> ApprovePoi(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound();
        poi.ApprovalStatus = "Approved";
        poi.IsActive = true;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{id}/reject")]
    public async Task<IActionResult> RejectPoi(int id)
    {
        var poi = await _context.POIs.FindAsync(id);
        if (poi == null) return NotFound();
        poi.ApprovalStatus = "Rejected";
        poi.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost]
    public async Task<ActionResult<POI>> CreatePOI(POI poi)
    {
        // Auto-generate Code nếu chưa có
        if (string.IsNullOrEmpty(poi.Code))
        {
            poi.Code = await _keyService.GeneratePOICodeAsync();
        }

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

        var existing = await _context.POIs.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (existing != null)
            poi.Code = existing.Code; // Giữ nguyên Code cũ

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

        var tourLinks = _context.TourPOIs.Where(t => t.POIId == id);
        _context.TourPOIs.RemoveRange(tourLinks);
        var audios = _context.Audios.Where(a => a.POIId == id);
        _context.Audios.RemoveRange(audios);
        var favs = _context.FavoritePOIs.Where(f => f.POIId == id);
        _context.FavoritePOIs.RemoveRange(favs);
        var narr = _context.NarrationLogs.Where(n => n.POIId == id);
        _context.NarrationLogs.RemoveRange(narr);

        _context.POIs.Remove(poi);
        await _context.SaveChangesAsync();

        return NoContent(); 
    }
}
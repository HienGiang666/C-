using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AudioController : ControllerBase
{
    private readonly AppDbContext _context;

    public AudioController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Audio>>> GetAudios([FromQuery] int? poiId)
    {
        if (poiId.HasValue)
        {
            return await _context.Audios.Where(a => a.POIId == poiId.Value).ToListAsync();
        }
        return await _context.Audios.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Audio>> GetAudio(int id)
    {
        var audio = await _context.Audios.FindAsync(id);

        if (audio == null)
        {
            return NotFound("Không tìm thấy audio!");
        }

        return audio;
    }

    [HttpPost]
    public async Task<ActionResult<Audio>> CreateAudio(Audio audio)
    {
        _context.Audios.Add(audio);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAudio), new { id = audio.Id }, audio);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAudio(int id, Audio audio)
    {
        if (id != audio.Id)
        {
            return BadRequest("ID không khớp!");
        }

        _context.Entry(audio).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Audios.Any(e => e.Id == id))
                return NotFound("Không tìm thấy audio để sửa!");
            else
                throw;
        }

        return NoContent(); 
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        var audio = await _context.Audios.FindAsync(id);
        if (audio == null)
        {
            return NotFound("Không tìm thấy audio để xóa!");
        }

        _context.Audios.Remove(audio);
        await _context.SaveChangesAsync();

        return NoContent(); 
    }
}

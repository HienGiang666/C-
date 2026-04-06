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
            return await _context.Audios.Where(a => a.POIId == poiId.Value).ToListAsync();
        return await _context.Audios.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Audio>> GetAudio(int id)
    {
        var audio = await _context.Audios.FindAsync(id);
        if (audio == null) return NotFound("Không tìm thấy audio!");
        return audio;
    }

    /// <summary>
    /// GET /api/audio/by-poi/{poiId}/lang/{lang}
    /// Mobile dùng để lấy ScriptText đúng ngôn ngữ hiện tại của user.
    /// lang: "vi", "en", "zh", "ja", "ko", "fr", "es", "de", "th", "ru"
    /// Tự động fallback về "vi" nếu không tìm thấy ngôn ngữ yêu cầu.
    /// </summary>
    [HttpGet("by-poi/{poiId}/lang/{lang}")]
    public async Task<ActionResult<Audio>> GetAudioByPoiAndLang(int poiId, string lang)
    {
        var audio = await _context.Audios
            .Where(a => a.POIId == poiId && a.Language == lang && a.IsActive)
            .FirstOrDefaultAsync();

        if (audio == null)
        {
            // Fallback về tiếng Việt
            audio = await _context.Audios
                .Where(a => a.POIId == poiId && a.Language == "vi" && a.IsActive)
                .FirstOrDefaultAsync();
        }

        if (audio == null) return NotFound();
        return audio;
    }

    [HttpPost]
    public async Task<ActionResult<Audio>> CreateAudio(Audio audio)
    {
        _context.Audios.Add(audio);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAudio), new { id = audio.Id }, audio);
    }

    /// <summary>
    /// POST /api/audio/bulk
    /// CMS dùng khi auto-translate: gửi array Audio nhiều ngôn ngữ cùng lúc.
    /// Xóa TTS cũ (AudioPath == "TTS_ONLY") của POI trước khi insert mới.
    /// Audio file MP3 thật sẽ được giữ nguyên (AudioPath != "TTS_ONLY").
    /// </summary>
    [HttpPost("bulk")]
    public async Task<IActionResult> BulkCreateAudio([FromBody] List<Audio> audios)
    {
        if (audios == null || audios.Count == 0)
            return BadRequest("Danh sách audio rỗng!");

        var poiId = audios[0].POIId;

        // Xóa các bản TTS_ONLY cũ của POI này
        var oldTts = await _context.Audios
            .Where(a => a.POIId == poiId && a.AudioPath == "TTS_ONLY")
            .ToListAsync();
        _context.Audios.RemoveRange(oldTts);

        foreach (var audio in audios)
        {
            audio.Id = 0; // reset ID để EF tự sinh
            if (string.IsNullOrEmpty(audio.AudioPath))
                audio.AudioPath = "TTS_ONLY";
            _context.Audios.Add(audio);
        }

        await _context.SaveChangesAsync();
        return Ok(new { inserted = audios.Count, poiId });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAudio(int id, Audio audio)
    {
        if (id != audio.Id) return BadRequest("ID không khớp!");
        _context.Entry(audio).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Audios.Any(e => e.Id == id))
                return NotFound("Không tìm thấy audio để sửa!");
            throw;
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAudio(int id)
    {
        var audio = await _context.Audios.FindAsync(id);
        if (audio == null) return NotFound("Không tìm thấy audio để xóa!");
        _context.Audios.Remove(audio);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

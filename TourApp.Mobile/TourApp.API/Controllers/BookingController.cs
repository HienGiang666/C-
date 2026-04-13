using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BookingController : ControllerBase
{
    private readonly AppDbContext _context;

    public BookingController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Booking>>> GetBookings()
    {
        return await _context.Bookings.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Booking>> GetBooking(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound();
        return booking;
    }

    [HttpPost]
    public async Task<ActionResult<Booking>> CreateBooking(Booking booking)
    {
        // Auto-generate Code nếu chưa có
        if (string.IsNullOrEmpty(booking.Code))
        {
            var maxCodeNum = await _context.Bookings
                .Select(b => b.Code)
                .ToListAsync();
            var nextNum = maxCodeNum
                .Where(c => !string.IsNullOrEmpty(c) && c!.StartsWith("BK-"))
                .Select(c => int.TryParse(c!.Substring(3), out var n) ? n : 0)
                .DefaultIfEmpty(0)
                .Max() + 1;
            booking.Code = $"BK-{nextNum}";
        }
        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBooking(int id, Booking booking)
    {
        if (id != booking.Id) return BadRequest();
        var existing = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (existing != null)
            booking.Code = existing.Code; // Giữ nguyên Code cũ
        _context.Entry(booking).State = EntityState.Modified;
        
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Bookings.Any(e => e.Id == id))
                return NotFound();
            else
                throw;
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

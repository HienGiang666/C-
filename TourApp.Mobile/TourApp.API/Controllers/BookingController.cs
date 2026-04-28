using Microsoft.AspNetCore.Authorization;
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

    [HttpGet("user/{userId}")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Booking>>> GetUserBookings(int userId)
    {
        // Chỉ cho phép user xem booking của chính mình
        var authUserIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!int.TryParse(authUserIdClaim, out int authUserId) || authUserId != userId)
            return Forbid();

        var bookings = await _context.Bookings
            .Where(b => b.UserId == userId)
            .Include(b => b.Tour)
            .Include(b => b.Payments)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
        return Ok(bookings);
    }

    /// <summary>
    /// GET /api/booking/admin/all
    /// Admin xem tất cả bookings với payment info
    /// </summary>
    [HttpGet("admin/all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetAllBookings()
    {
        var bookings = await _context.Bookings
            .Include(b => b.Tour)
            .Include(b => b.User)
            .Include(b => b.Payments)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
        return Ok(bookings);
    }

    /// <summary>
    /// POST /api/booking
    /// Chỉ khách hàng đã đăng nhập mới được đặt tour
    /// </summary>
    [HttpPost]
    [Authorize] // Yêu cầu đăng nhập
    public async Task<ActionResult<Booking>> CreateBooking(Booking booking)
    {
        try
        {
            // Lấy UserId từ token đăng nhập
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int authUserId))
            {
                return Unauthorized(new { message = "Vui lòng đăng nhập để đặt tour" });
            }
            
            // Đảm bảo UserId trong booking khớp với user đang đăng nhập
            if (booking.UserId != authUserId)
            {
                return Forbid(); // Không được đặt hộ người khác
            }
            
            // Validate required fields
            if (booking.TourId <= 0)
                return BadRequest(new { message = "Tour không hợp lệ" });
            
            if (booking.NumberOfParticipants <= 0)
                return BadRequest(new { message = "Số lượng người phải lớn hơn 0" });
            
            // Check if tour exists
            var tour = await _context.Tours.FindAsync(booking.TourId);
            if (tour == null)
                return BadRequest(new { message = "Tour không tồn tại" });
            
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
            
            booking.BookingDate = DateTime.Now;
            booking.Status = "Pending"; // Mặc định chờ xác nhận
            
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            
            return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[BookingController] Error: {ex.Message}");
            Console.WriteLine($"[BookingController] Stack: {ex.StackTrace}");
            return StatusCode(500, new { message = $"Lỗi server: {ex.Message}" });
        }
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

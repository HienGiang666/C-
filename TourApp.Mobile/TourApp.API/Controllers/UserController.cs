using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using System.Security.Cryptography;
using System.Text;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserController(AppDbContext context)
    {
        _context = context;
    }

    // POST /api/user/login  - dùng bởi CMS để xác thực
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var hash = HashPassword(req.Password);
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.PasswordHash == hash && u.IsActive);
        if (user == null)
            return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu!" });
        return Ok(new
        {
            user.Id,
            user.Username,
            user.FullName,
            user.Email,
            user.Role
        });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<User>> GetUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        return user;
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(User user)
    {
        // Hash nếu plain text được truyền lên
        if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.Length < 60)
            user.PasswordHash = HashPassword(user.PasswordHash);
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, User user)
    {
        if (id != user.Id) return BadRequest();
        _context.Entry(user).State = EntityState.Modified;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Users.Any(e => e.Id == id)) return NotFound();
            else throw;
        }
        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();
        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/favorites")]
    public async Task<ActionResult<IEnumerable<POI>>> GetFavorites(int id)
    {
        var favorites = await _context.FavoritePOIs
            .Where(f => f.UserId == id)
            .Include(f => f.POI)
            .Select(f => f.POI)
            .ToListAsync();
        return Ok(favorites);
    }

    [HttpPost("{id}/favorites/{poiId}")]
    public async Task<IActionResult> AddFavorite(int id, int poiId)
    {
        var exists = await _context.FavoritePOIs.AnyAsync(f => f.UserId == id && f.POIId == poiId);
        if (!exists)
        {
            _context.FavoritePOIs.Add(new FavoritePOI { UserId = id, POIId = poiId });
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpDelete("{id}/favorites/{poiId}")]
    public async Task<IActionResult> RemoveFavorite(int id, int poiId)
    {
        var fav = await _context.FavoritePOIs.FirstOrDefaultAsync(f => f.UserId == id && f.POIId == poiId);
        if (fav != null)
        {
            _context.FavoritePOIs.Remove(fav);
            await _context.SaveChangesAsync();
        }
        return Ok();
    }

    [HttpGet("{id}/bookings")]
    public async Task<ActionResult<IEnumerable<Booking>>> GetBookings(int id)
    {
        var bookings = await _context.Bookings
            .Where(b => b.UserId == id)
            .OrderByDescending(b => b.BookingDate)
            .ToListAsync();
        
        // Populate Tour names if needed, but we can just return bookings
        foreach(var booking in bookings)
        {
            var tour = await _context.Tours.FindAsync(booking.TourId);
            if (tour != null) booking.Notes = tour.Name; // Temporarily using Notes to pass Tour Name for UI
        }
        return Ok(bookings);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
    }
}

public record LoginRequest(string Username, string Password);

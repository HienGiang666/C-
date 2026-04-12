using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;
using System.Security.Cryptography;
using System.Text;

using TourApp.API.Helpers;

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
        try
        {
            var username = (req.Username ?? "").Trim().ToLower();
            var password = (req.Password ?? "").Trim();
            var hash = SecurityHelper.HashPassword(password);
            
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == username && u.IsActive);
            
            if (user == null || user.PasswordHash != hash)
                return Unauthorized(new { message = "Sai tên đăng nhập hoặc mật khẩu!" });

            user.LastLoginAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                user.Id,
                user.Username,
                user.FullName,
                user.Email,
                user.Role,
                user.LastLoginAt
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Lỗi hệ thống: " + ex.Message });
        }
    }

    [HttpGet("test-db")]
    public async Task<IActionResult> TestDb()
    {
        try
        {
            var count = await _context.Users.CountAsync();
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
            return Ok(new { 
                success = true, 
                userCount = count, 
                adminStatus = admin != null ? $"Found, Active={admin.IsActive}, Hash={admin.PasswordHash}" : "Not Found" 
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { success = false, error = ex.Message, inner = ex.InnerException?.Message });
        }
    }

    [HttpGet("force-reset-admin")]
    public async Task<IActionResult> ForceResetAdmin()
    {
        var admin = await _context.Users.FirstOrDefaultAsync(u => u.Username == "admin");
        var hash = SecurityHelper.HashPassword("admin123");
        if (admin == null)
        {
            admin = new User
            {
                Username = "admin",
                FullName = "Quản trị viên",
                PasswordHash = hash,
                Email = "admin@tourapp.vn",
                Role = "Admin",
                IsActive = true,
                Code = "#U1001"
            };
            _context.Users.Add(admin);
        }
        else
        {
            admin.PasswordHash = hash;
            admin.IsActive = true;
            admin.Role = "Admin";
        }
        await _context.SaveChangesAsync();
        return Ok(new { message = "Admin password reset to admin123", hash = hash });
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<User>>> GetUsers()
    {
        return await _context.Users
            .OrderBy(u => u.Code ?? string.Empty)
            .ThenBy(u => u.Id)
            .ToListAsync();
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
        user.Role = NormalizeRole(user.Role);
        // Hash nếu plain text được truyền lên
        if (!string.IsNullOrEmpty(user.PasswordHash) && user.PasswordHash.Length < 60)
            user.PasswordHash = SecurityHelper.HashPassword(user.PasswordHash);
        // Auto-generate Code nếu chưa có
        if (string.IsNullOrEmpty(user.Code))
        {
            var maxCodeNum = await _context.Users
                .Select(u => u.Code)
                .ToListAsync();
            var nextNum = maxCodeNum
                .Where(c => !string.IsNullOrEmpty(c) && c!.StartsWith("#U"))
                .Select(c => int.TryParse(c!.Substring(2), out var n) ? n : 0)
                .DefaultIfEmpty(1000)
                .Max() + 1;
            user.Code = $"#U{nextNum}";
        }

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, [FromBody] User user)
    {
        if (id != user.Id) return BadRequest();

        var existingUser = await _context.Users.FindAsync(id);
        if (existingUser == null) return NotFound();

        existingUser.FullName = user.FullName;
        existingUser.Email = user.Email;
        existingUser.PhoneNumber = user.PhoneNumber;
        existingUser.Address = user.Address;
        existingUser.DateOfBirth = user.DateOfBirth;
        existingUser.Role = NormalizeRole(user.Role);
        existingUser.IsActive = user.IsActive;

        // Cho phép Admin cập nhật mật khẩu nếu có truyền vào PasswordHash mới (ở dạng text chưa hash)
        if (!string.IsNullOrWhiteSpace(user.PasswordHash) && user.PasswordHash != existingUser.PasswordHash)
        {
            existingUser.PasswordHash = SecurityHelper.HashPassword(user.PasswordHash);
        }

        await _context.SaveChangesAsync();
        return Ok(existingUser);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null) return NotFound();

        // Kiểm tra xem user có đang sở hữu POI nào không
        var hasPOIs = await _context.POIs.AnyAsync(p => p.CreatedByUserId == id);
        
        if (hasPOIs)
        {
            // Nếu có POI, không xóa mà chỉ chuyển sang trạng thái ẩn
            user.IsActive = false;
            await _context.SaveChangesAsync();
            return Ok(new { message = "User has POIs, switched to inactive status instead of deleting." });
        }

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id}/favorites/full")]
    public async Task<ActionResult<IEnumerable<FavoritePOI>>> GetFullFavorites(int id)
    {
        return await _context.FavoritePOIs
            .Where(f => f.UserId == id)
            .ToListAsync();
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
            if (tour != null) booking.Notes = tour.Name ?? string.Empty; // Temporarily using Notes to pass Tour Name for UI
        }
        return Ok(bookings);
    }

    private static string NormalizeRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            return "Customer";
        if (role.Equals("Staff", StringComparison.OrdinalIgnoreCase))
            return "RestaurantOwner";
        return role;
    }
}

public record LoginRequest(string Username, string Password);

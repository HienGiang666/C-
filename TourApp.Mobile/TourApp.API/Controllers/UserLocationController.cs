using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UserLocationController : ControllerBase
{
    private readonly AppDbContext _context;

    public UserLocationController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserLocationLog>>> GetLogs()
    {
        return await _context.UserLocationLogs
            .OrderByDescending(l => l.Timestamp)
            .Take(100)
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<UserLocationLog>> LogLocation(UserLocationLog log)
    {
        log.Timestamp = DateTime.Now;
        _context.UserLocationLogs.Add(log);
        await _context.SaveChangesAsync();
        return Ok(log);
    }
}

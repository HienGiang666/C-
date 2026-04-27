using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Data;
using TourApp.API.Models;

namespace TourApp.API.Controllers;

/// <summary>
/// Controller quản lý danh mục POI
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class CategoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public CategoryController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// GET /api/category
    /// Lấy tất cả danh mục (đang active)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync();

        return Ok(categories);
    }

    /// <summary>
    /// GET /api/category/{id}
    /// Lấy chi tiết 1 danh mục
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<Category>> GetCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound();

        return Ok(category);
    }

    /// <summary>
    /// GET /api/category/{id}/pois
    /// Lấy tất cả POI thuộc danh mục
    /// </summary>
    [HttpGet("{id}/pois")]
    public async Task<ActionResult<IEnumerable<POI>>> GetCategoryPOIs(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound(new { message = "Không tìm thấy danh mục" });

        var pois = await _context.POICategories
            .Where(pc => pc.CategoryId == id)
            .Include(pc => pc.POI)
            .Where(pc => pc.POI != null && pc.POI.IsActive && pc.POI.ApprovalStatus == "Approved")
            .Select(pc => pc.POI)
            .ToListAsync();

        return Ok(pois);
    }

    /// <summary>
    /// POST /api/category
    /// Tạo danh mục mới (Admin only)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<Category>> CreateCategory(Category category)
    {
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
    }

    /// <summary>
    /// PUT /api/category/{id}
    /// Cập nhật danh mục
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCategory(int id, Category category)
    {
        if (id != category.Id)
            return BadRequest();

        _context.Entry(category).State = EntityState.Modified;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Categories.Any(e => e.Id == id))
                return NotFound();
            else
                throw;
        }

        return NoContent();
    }

    /// <summary>
    /// DELETE /api/category/{id}
    /// Xóa danh mục (soft delete)
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
            return NotFound();

        category.IsActive = false;
        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// POST /api/category/assign
    /// Gán POI vào danh mục
    /// </summary>
    [HttpPost("assign")]
    public async Task<ActionResult> AssignPOIToCategory([FromBody] AssignCategoryRequest request)
    {
        var poi = await _context.POIs.FindAsync(request.POIId);
        if (poi == null)
            return NotFound(new { message = "Không tìm thấy POI" });

        var category = await _context.Categories.FindAsync(request.CategoryId);
        if (category == null)
            return NotFound(new { message = "Không tìm thấy danh mục" });

        // Kiểm tra xem đã gán chưa
        var existing = await _context.POICategories
            .FirstOrDefaultAsync(pc => pc.POIId == request.POIId && pc.CategoryId == request.CategoryId);

        if (existing != null)
            return BadRequest(new { message = "POI đã thuộc danh mục này" });

        var poiCategory = new POICategory
        {
            POIId = request.POIId,
            CategoryId = request.CategoryId
        };

        _context.POICategories.Add(poiCategory);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Gán danh mục thành công" });
    }

    /// <summary>
    /// DELETE /api/category/assign
    /// Xóa POI khỏi danh mục
    /// </summary>
    [HttpDelete("assign")]
    public async Task<IActionResult> RemovePOIFromCategory([FromBody] AssignCategoryRequest request)
    {
        var poiCategory = await _context.POICategories
            .FirstOrDefaultAsync(pc => pc.POIId == request.POIId && pc.CategoryId == request.CategoryId);

        if (poiCategory == null)
            return NotFound(new { message = "POI không thuộc danh mục này" });

        _context.POICategories.Remove(poiCategory);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}

/// <summary>
/// Request model cho gán/xóa POI khỏi category
/// </summary>
public class AssignCategoryRequest
{
    public int POIId { get; set; }
    public int CategoryId { get; set; }
}

using Microsoft.AspNetCore.Mvc;

namespace TourApp.CMS.Controllers;

/// <summary>
/// Proxy endpoint dùng để gọi Google Translate từ server-side,
/// tránh lỗi CORS khi gọi từ JavaScript client.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class TranslateController : ControllerBase
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    [HttpGet]
    public async Task<IActionResult> Translate([FromQuery] string text, [FromQuery] string target, [FromQuery] string source = "auto")
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(target))
            return BadRequest("Missing text or target");

        try
        {
            var url = $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={source}&tl={target}&dt=t&q={Uri.EscapeDataString(text)}";
            var response = await _http.GetStringAsync(url);
            return Content(response, "application/json");
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }
}

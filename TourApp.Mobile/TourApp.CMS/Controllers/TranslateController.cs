using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TourApp.CMS.Controllers;

/// <summary>
/// Proxy endpoint dùng để gọi dịch từ server-side,
/// tránh lỗi CORS khi gọi từ JavaScript client.
/// Dùng LibreTranslate làm primary, Google làm fallback.
/// </summary>
[Route("api/translate")]
[ApiController]
[Produces("application/json")]
public class TranslateController : ControllerBase
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    [HttpGet]
    public async Task<IActionResult> Translate(
        [FromQuery] string text,
        [FromQuery] string target,
        [FromQuery] string source = "auto")
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(target))
                return BadRequest(new { error = "Missing text or target" });

            var srcLang = source?.ToLower() == "auto"
                ? await DetectLanguageAsync(text)
                : source;

            if (string.IsNullOrWhiteSpace(srcLang))
                srcLang = "vi";

            Console.WriteLine($"[Translate API] text={text.Substring(0, Math.Min(30, text.Length))}..., target={target}, source={srcLang}");

            var libreResult = await TryLibreTranslateAsync(text, srcLang, target);
            if (!string.IsNullOrWhiteSpace(libreResult))
            {
                Console.WriteLine("[Translate API] LibreTranslate success");
                return Content(libreResult, "application/json", Encoding.UTF8);
            }

            var googleResult = await TryGoogleTranslateAsync(text, srcLang, target);
            if (!string.IsNullOrWhiteSpace(googleResult))
            {
                Console.WriteLine("[Translate API] Google Translate success");
                return Content(googleResult, "application/json", Encoding.UTF8);
            }

            Console.WriteLine("[Translate API] All services failed, returning original text");
            return Content(BuildGoogleLikeJson(text, text, srcLang), "application/json", Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Translate API] Exception: {ex}");
            return Content(BuildGoogleLikeJson(text ?? "", text ?? "", "auto"), "application/json", Encoding.UTF8);
        }
    }

    private Task<string> DetectLanguageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult("vi");
        if (Regex.IsMatch(text, @"[\u4e00-\u9fff]")) return Task.FromResult("zh");
        if (Regex.IsMatch(text, @"[\u3040-\u30ff]")) return Task.FromResult("ja");
        if (Regex.IsMatch(text, @"[\uac00-\ud7af]")) return Task.FromResult("ko");
        if (Regex.IsMatch(text, @"[àáâãèéêìíòóôõùúýăđơưạảấầẩẫậắằẳẵặẹẻẽếềểễệỉịọỏốồổỗộớờởỡợụủứừửữựỳỵỷỹ]")) return Task.FromResult("vi");
        return Task.FromResult("en");
    }

    private async Task<string?> TryLibreTranslateAsync(string text, string source, string target)
    {
        if (string.IsNullOrWhiteSpace(source))
            source = "vi";

        var libreUrls = new[]
        {
            "https://libretranslate.de/translate",
            "https://translate.argosopentech.com/translate"
        };

        foreach (var url in libreUrls)
        {
            try
            {
                var requestBody = new
                {
                    q = text,
                    source,
                    target,
                    format = "text"
                };

                var json = JsonSerializer.Serialize(requestBody);
                using var content = new StringContent(json, Encoding.UTF8, "application/json");

                Console.WriteLine($"[LibreTranslate] Trying {url}...");
                using var response = await _http.PostAsync(url, content);

                var raw = await response.Content.ReadAsStringAsync();
                var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";

                Console.WriteLine($"[LibreTranslate] {url} Status: {response.StatusCode}");
                Console.WriteLine($"[LibreTranslate] {url} Content-Type: {mediaType}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[LibreTranslate] {url} Error: {raw.Substring(0, Math.Min(150, raw.Length))}");
                    continue;
                }

                if (!mediaType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"[LibreTranslate] {url} Non-JSON response: {raw.Substring(0, Math.Min(150, raw.Length))}");
                    continue;
                }

                LibreTranslateResponse? result;
                try
                {
                    result = JsonSerializer.Deserialize<LibreTranslateResponse>(raw, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LibreTranslate] {url} JSON parse failed: {ex.Message}");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(result?.TranslatedText))
                {
                    Console.WriteLine($"[LibreTranslate] Success: {result.TranslatedText.Substring(0, Math.Min(30, result.TranslatedText.Length))}...");
                    return BuildGoogleLikeJson(result.TranslatedText, text, source);
                }

                Console.WriteLine($"[LibreTranslate] {url} JSON parsed but no translatedText");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LibreTranslate] {url} Exception: {ex.Message}");
            }
        }

        return null;
    }

    private async Task<string?> TryGoogleTranslateAsync(string text, string source, string target)
    {
        try
        {
            var url =
                $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={Uri.EscapeDataString(source)}&tl={Uri.EscapeDataString(target)}&dt=t&q={Uri.EscapeDataString(text)}";

            return await _http.GetStringAsync(url);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Google Translate] Failed: {ex.Message}");
            return null;
        }
    }

    private static string BuildGoogleLikeJson(string translated, string original, string source)
    {
        return JsonSerializer.Serialize(new object?[]
        {
            new object?[]
            {
                new object?[] { translated, original, null, null, 1 }
            },
            null,
            source
        });
    }

    private sealed class LibreTranslateResponse
    {
        public string? TranslatedText { get; set; }
    }
}
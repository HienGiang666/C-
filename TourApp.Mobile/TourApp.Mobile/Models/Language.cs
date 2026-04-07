using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class Language
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("nativeName")]
        public string NativeName { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("flag")]
        public string? Flag { get; set; }
    }
}

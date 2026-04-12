using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class Audio
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("poiId")]
        public int POIId { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; } = "vi";

        [JsonPropertyName("audioPath")]
        public string? AudioPath { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("scriptText")]
        public string? ScriptText { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

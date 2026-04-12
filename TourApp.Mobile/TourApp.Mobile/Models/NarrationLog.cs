using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class NarrationLog
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("deviceId")]
        public string? DeviceId { get; set; }

        [JsonPropertyName("poiId")]
        public int POIId { get; set; }

        [JsonPropertyName("audioId")]
        public int? AudioId { get; set; }

        [JsonPropertyName("playedAt")]
        public DateTime PlayedAt { get; set; }

        [JsonPropertyName("triggerType")]
        public string? TriggerType { get; set; }
    }
}

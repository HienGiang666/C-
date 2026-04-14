using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class TourPOI
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tourId")]
        public int TourId { get; set; }

        [JsonPropertyName("poiId")]
        public int POIId { get; set; }

        [JsonPropertyName("orderIndex")]
        public int OrderIndex { get; set; } = 0;

        [JsonPropertyName("poi")]
        public POI? POI { get; set; }
    }
}

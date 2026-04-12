using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class Tour
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("destination")]
        public string? Destination { get; set; }

        [JsonPropertyName("maxParticipants")]
        public int MaxParticipants { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("searchKeywords")]
        public string? SearchKeywords { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        /// <summary>Chỉ UI (compiled binding); không map từ API.</summary>
        [JsonIgnore]
        public int PoiCount { get; set; }

        /// <summary>Chỉ UI (compiled binding); không map từ API.</summary>
        [JsonIgnore]
        public double Distance { get; set; }
    }
}

using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class POI
    {
        [JsonPropertyName("id")]
        public int PoiId { get; set; }

        [JsonPropertyName("name")]
        public string PoiName { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("address")]
        public string Address { get; set; } = string.Empty;

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("radius")]
        public double Radius { get; set; } = 80;

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("imageUrl")]
        public string ImageUrl { get; set; } = string.Empty;

        [JsonPropertyName("openTime")]
        public string OpenTime { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("rating")]
        public double Rating { get; set; } = 4.5;

        /// <summary>
        /// Danh sách audio/script đa ngôn ngữ từ DB (đã được CMS auto-translate).
        /// API trả về cùng POI (Include Audios).
        /// Mobile dùng để phát TTS đúng ngôn ngữ user đang chọn.
        /// </summary>
        [JsonPropertyName("audios")]
        public List<Audio> Audios { get; set; } = new();

        /// <summary>
        /// Lấy ScriptText theo ngôn ngữ, fallback về "vi", fallback cuối là Description.
        /// </summary>
        public string GetScript(string lang = "vi")
        {
            var audio = Audios.FirstOrDefault(a => a.Language == lang && a.IsActive && !string.IsNullOrWhiteSpace(a.ScriptText));
            if (audio != null) return audio.ScriptText;

            // Fallback về tiếng Việt
            var viAudio = Audios.FirstOrDefault(a => a.Language == "vi" && a.IsActive && !string.IsNullOrWhiteSpace(a.ScriptText));
            if (viAudio != null) return viAudio.ScriptText;

            // Fallback cuối: dùng Description
            return $"Chào mừng bạn đến {PoiName}. {Description}";
        }
    }
}
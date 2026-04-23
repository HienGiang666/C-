using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class POI
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }

        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }

        [JsonPropertyName("radius")]
        public double Radius { get; set; } = 80;

        [JsonPropertyName("priority")]
        public int Priority { get; set; }

        [JsonPropertyName("imageUrl")]
        public string? ImageUrl { get; set; }

        [JsonPropertyName("openTime")]
        public string? OpenTime { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonPropertyName("rating")]
        public double Rating { get; set; } = 4.5;

        [JsonPropertyName("approvalStatus")]
        public string? ApprovalStatus { get; set; }

        [JsonPropertyName("ownerUserId")]
        public int? OwnerUserId { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        [JsonPropertyName("displayCode")]
        public string DisplayCode => string.IsNullOrEmpty(Code) ? $"#P{Id}" : Code;

        [JsonPropertyName("audios")]
        public List<Audio> Audios { get; set; } = new();

        [JsonPropertyName("translations")]
        public List<POITranslation> Translations { get; set; } = new();

        /// <summary>
        /// Lấy Description theo ngôn ngữ đang chọn, fallback về vi.
        /// </summary>
        public string GetLocalizedDescription(string lang = "vi")
        {
            if (lang == "vi") return Description ?? "";
            var t = Translations.FirstOrDefault(x => x.Language == lang);
            if (t != null && !string.IsNullOrWhiteSpace(t.Description)) return t.Description;
            return Description ?? "";
        }

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
        return $"Chào mừng bạn đến {Name}. {Description}";
    }
}

public class POITranslation
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("poiId")]
    public int POIId { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; } = "en";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public class POIMapDto
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
}

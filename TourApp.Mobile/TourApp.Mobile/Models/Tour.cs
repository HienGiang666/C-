using System.Text.Json.Serialization;
using TourApp.Mobile.Services;

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

        [JsonPropertyName("translations")]
        public List<TourTranslation> Translations { get; set; } = new();

        /// <summary>Chỉ UI (compiled binding); không map từ API.</summary>
        [JsonIgnore]
        public int PoiCount { get; set; }

        /// <summary>Chỉ UI (compiled binding); không map từ API.</summary>
        [JsonIgnore]
        public double Distance { get; set; }

        /// <summary>
        /// Description đã localize theo ngôn ngữ hiện tại của app. Dùng cho binding UI.
        /// </summary>
        [JsonIgnore]
        public string LocalizedDescription => GetLocalizedDescription(LanguageService.CurrentLanguage);

        /// <summary>
        /// Lấy Description theo ngôn ngữ, fallback về vi, fallback cuối là Description gốc.
        /// </summary>
        public string GetLocalizedDescription(string lang = "vi")
        {
            if (string.IsNullOrWhiteSpace(lang) || lang.Equals("vi", StringComparison.OrdinalIgnoreCase))
                return Description ?? "";

            var normalizedLang = lang.Split('-')[0].ToLowerInvariant();

            if (Translations == null || !Translations.Any())
                return Description ?? "";

            var t = Translations.FirstOrDefault(x =>
                x.Language != null &&
                x.Language.Equals(lang, StringComparison.OrdinalIgnoreCase));

            if (t == null)
            {
                t = Translations.FirstOrDefault(x =>
                    x.Language != null &&
                    x.Language.Split('-')[0].ToLowerInvariant() == normalizedLang);
            }

            if (t != null && !string.IsNullOrWhiteSpace(t.Description))
                return t.Description;

            return Description ?? "";
        }
    }

    public class TourTranslation
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tourId")]
        public int TourId { get; set; }

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }
}

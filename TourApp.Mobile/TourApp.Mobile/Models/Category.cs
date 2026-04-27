using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    /// <summary>
    /// Danh mục phân loại POI (Nướng, Lẩu, Ốc, Ăn vặt...)
    /// </summary>
    public class Category
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("icon")]
        public string? Icon { get; set; }
        
        [JsonPropertyName("color")]
        public string? Color { get; set; }
        
        [JsonPropertyName("displayOrder")]
        public int DisplayOrder { get; set; }
        
        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; } = true;
    }
}

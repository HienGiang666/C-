namespace TourApp.CMS.Models
{
    /// <summary>
    /// Danh mục phân loại POI (Nướng, Lẩu, Ốc, Ăn vặt...)
    /// </summary>
    public class Category
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;     // Nướng, Lẩu, Ốc...
        public string? Icon { get; set; }                    // Icon class name
        public string? Color { get; set; }                     // #FF6B35
        public int DisplayOrder { get; set; } = 0;           // Thứ tự hiển thị
        public bool IsActive { get; set; } = true;

        // Navigation property
        public ICollection<POICategory>? POICategories { get; set; } = new List<POICategory>();
    }
}

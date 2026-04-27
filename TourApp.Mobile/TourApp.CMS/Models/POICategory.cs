namespace TourApp.CMS.Models
{
    /// <summary>
    /// Bảng liên kết nhiều-nhiều giữa POI và Category
    /// </summary>
    public class POICategory
    {
        public int Id { get; set; }
        public int POIId { get; set; }
        public int CategoryId { get; set; }

        // Navigation properties
        public POI? POI { get; set; }
        public Category? Category { get; set; }
    }
}

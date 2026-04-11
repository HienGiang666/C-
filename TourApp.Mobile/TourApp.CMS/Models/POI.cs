namespace TourApp.CMS.Models
{
    public class POI
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        
        // Bổ sung các trường để không bị mất data khi CMS gọi API Update
        public string Address { get; set; } = string.Empty;
        public double Radius { get; set; } = 80;
        public int Priority { get; set; } = 1;
        public string ImageUrl { get; set; } = string.Empty;
        public string OpenTime { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public double Rating { get; set; } = 4.5;
        public string ApprovalStatus { get; set; } = "Approved";
        public int? OwnerUserId { get; set; }
        public int PublicCatalogNumber { get; set; }
    }
}
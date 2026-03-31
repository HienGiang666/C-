namespace TourApp.Mobile.Models
{
    public class Tour
    {
        public int TourId { get; set; }
        public string TourName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<POI> POIs { get; set; } = new();
    }
}
namespace TourApp.Mobile.Models
{
    public class Tour
    {
        public int TourId { get; set; }
        public string TourName { get; set; }
        public string Description { get; set; }
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<POI> POIs { get; set; } = new();
    }
}
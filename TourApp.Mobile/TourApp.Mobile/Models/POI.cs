namespace TourApp.Mobile.Models
{
    public class POI
    {
        public int PoiId { get; set; }
        public string PoiName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } = 80;
        public int Priority { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string OpenTime { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public double Rating { get; set; } = 4.5;
        public List<Audio> Audios { get; set; } = new();
    }
}
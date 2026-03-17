namespace TourApp.Mobile.Models
{
    public class POI
    {
        public int PoiId { get; set; }
        public string PoiName { get; set; }
        public string Description { get; set; }
        public string Address { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } = 80;
        public int Priority { get; set; }
        public string ImageUrl { get; set; }
        public string OpenTime { get; set; }
        public bool IsActive { get; set; }
        public List<Audio> Audios { get; set; } = new();
    }
}
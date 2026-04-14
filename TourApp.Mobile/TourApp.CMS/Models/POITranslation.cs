namespace TourApp.CMS.Models
{
    public class POITranslation
    {
        public int Id { get; set; }
        public int POIId { get; set; }
        public string Language { get; set; } = "en";
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}

namespace TourApp.CMS.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int POIId { get; set; }
        public string Language { get; set; } = "vi";
        public string AudioPath { get; set; } = string.Empty;
        public string ScriptText { get; set; } = string.Empty;
        public int Duration { get; set; }
        public bool IsActive { get; set; } = true;
    }
}

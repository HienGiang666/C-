namespace TourApp.Mobile.Models
{
    public class Audio
    {
        public int AudioId { get; set; }
        public int PoiId { get; set; }
        public string Language { get; set; } = "vi";
        public string AudioPath { get; set; }
        public string ScriptText { get; set; }
        public int Duration { get; set; }
        public bool IsActive { get; set; }
    }
}
namespace TourApp.Mobile.Models
{
    public class NarrationLog
    {
        public int LogId { get; set; }
        public string DeviceId { get; set; } = string.Empty;
        public int PoiId { get; set; }
        public int? AudioId { get; set; }
        public DateTime PlayedAt { get; set; }
        public string TriggerType { get; set; } = string.Empty;
    }
}
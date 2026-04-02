namespace TourApp.API.Models;

public class NarrationLog
{
    public int Id { get; set; }
    public int POIId { get; set; }
    public int? AudioId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string DeviceId { get; set; } = string.Empty;
}

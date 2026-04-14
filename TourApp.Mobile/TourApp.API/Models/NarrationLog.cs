using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models;

public class NarrationLog
{
    [Key]
    public int Id { get; set; }
    public int POIId { get; set; }
    public int? AudioId { get; set; }
    public string? TriggerType { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? DeviceId { get; set; }
    
    // Duration listened in seconds (for analytics) - Ignore on DB save due to missing column
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int DurationListened { get; set; } = 0;
}

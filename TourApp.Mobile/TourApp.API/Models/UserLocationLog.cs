using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models;

public class UserLocationLog
{
    [Key]
    public int Id { get; set; }
    public string? DeviceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string? SessionId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsMock { get; set; } = false;
}

namespace TourApp.API.Models;

public class UserLocationLog
{
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

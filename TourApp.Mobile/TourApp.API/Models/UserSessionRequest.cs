namespace TourApp.API.Models;

public class UserSessionRequest
{
    public int? UserId { get; set; }
    public string? GuestId { get; set; }
    public bool IsOnline { get; set; }
    public string? Name { get; set; }
    public string? DeviceInfo { get; set; }
    public string? Platform { get; set; }
    public string? Version { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
}

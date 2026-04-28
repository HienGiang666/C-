namespace TourApp.API.Models;

public class SaveRouteRequest
{
    public string? DeviceId { get; set; }
    public int? UserId { get; set; }
    public List<double[]>? Coordinates { get; set; } // [ [lng, lat], ... ]
}

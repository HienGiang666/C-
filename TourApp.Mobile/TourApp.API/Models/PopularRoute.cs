using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models;

public class PopularRoute
{
    [Key]
    public int Id { get; set; }

    public string? DeviceId { get; set; }
    public int? UserId { get; set; }

    public double OriginLat { get; set; }
    public double OriginLng { get; set; }
    public double DestLat { get; set; }
    public double DestLng { get; set; }

    /// <summary>Encoded polyline của toàn bộ tuyến đường (Goong/OSRM style)</summary>
    public string? Polyline { get; set; }

    /// <summary>Số lần được sử dụng (dùng để tô màu)</summary>
    public int UseCount { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime LastUsedAt { get; set; } = DateTime.Now;
}

namespace TourApp.API.Models;

public class POI
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Radius { get; set; } = 80;
    public int Priority { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public string OpenTime { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public double Rating { get; set; } = 4.5;

    /// <summary>Approved | Pending | Rejected — POI mới của chủ quán chờ Admin duyệt.</summary>
    public string ApprovalStatus { get; set; } = "Approved";

    public int? OwnerUserId { get; set; }

    /// <summary>Mã nghiệp vụ #P1, #P2... (Business Key, VARCHAR).</summary>
    public string? Code { get; set; }

    /// <summary>Mã hiển thị đầy đủ (VD: #P1).</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DisplayCode => string.IsNullOrEmpty(Code) ? $"#P{Id}" : Code;

    public ICollection<Audio> Audios { get; set; } = new List<Audio>();
}
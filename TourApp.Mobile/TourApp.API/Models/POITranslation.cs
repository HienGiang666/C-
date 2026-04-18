using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TourApp.API.Models;

public class POITranslation
{
    [Key]
    public int Id { get; set; }
    public int POIId { get; set; }
    public string Language { get; set; } = "en";
    public string? Name { get; set; }
    public string? Description { get; set; }

    [JsonIgnore]
    public virtual POI? POI { get; set; }
}

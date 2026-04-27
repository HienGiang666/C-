using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TourApp.API.Models;

public class TourTranslation
{
    [Key]
    public int Id { get; set; }
    public int TourId { get; set; }
    public string Language { get; set; } = "vi";
    public string? Description { get; set; }

    [JsonIgnore]
    public virtual Tour? Tour { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace TourApp.CMS.Models;

public class AudioBulkCreateViewModel
{
    [Required]
    public int POIId { get; set; }

    public string SourceText { get; set; } = string.Empty;

    public Dictionary<string, string> Scripts { get; set; } = new();
}

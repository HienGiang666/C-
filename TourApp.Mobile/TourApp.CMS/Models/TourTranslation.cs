namespace TourApp.CMS.Models;

public class TourTranslation
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public string Language { get; set; } = "vi";
    public string? Description { get; set; }
}

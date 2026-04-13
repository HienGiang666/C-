using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models;

public class TourPOI
{
    [Key]
    public int Id { get; set; }
    public int TourId { get; set; }
    public int POIId { get; set; }
    public int OrderIndex { get; set; } = 0;

    public Tour? Tour { get; set; }
    public POI? POI { get; set; }
}

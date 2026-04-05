namespace TourApp.API.Models;

public class TourPOI
{
    public int Id { get; set; }
    public int TourId { get; set; }
    public int POIId { get; set; }
    public int OrderIndex { get; set; }

    public Tour? Tour { get; set; }
    public POI? POI { get; set; }
}

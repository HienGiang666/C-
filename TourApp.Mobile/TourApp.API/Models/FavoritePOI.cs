using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace TourApp.API.Models
{
    public class FavoritePOI
    {
        [Key]
        public int Id { get; set; }
        public int UserId { get; set; }
        public int POIId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [JsonIgnore]
        public User? User { get; set; }

        public POI? POI { get; set; }
    }
}

using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int POIId { get; set; }
        public string? Language { get; set; } = "vi";
        public string? AudioPath { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Thời lượng âm thanh phải >= 0")]
        public int Duration { get; set; }
        
        public string? ScriptText { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}


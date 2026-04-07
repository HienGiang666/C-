using System.ComponentModel.DataAnnotations;

namespace TourApp.Mobile.Models
{
    public class Audio
    {
        public int Id { get; set; }
        public int POIId { get; set; }
        public string Language { get; set; } = "vi";
        public string AudioPath { get; set; } = string.Empty;
        
        [Range(1, int.MaxValue, ErrorMessage = "Th?i lu?ng âm thanh ph?i l?n hon 0")]
        public int Duration { get; set; }
        
        public string ScriptText { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}


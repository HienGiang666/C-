using System.ComponentModel.DataAnnotations;

namespace TourApp.Mobile.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        [Range(1, double.MaxValue, ErrorMessage = "Giá Tour ph?i l?n hon 0")]
        public decimal Price { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Th?i lu?ng t?i thi?u là 1 ngày")]
        public int Duration { get; set; } // S? ngày
        
        public string Destination { get; set; } = string.Empty;
        
        [Range(1, int.MaxValue, ErrorMessage = "S? khách tham gia t?i thi?u là 1")]
        public int MaxParticipants { get; set; }
        
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string SearchKeywords { get; set; } = string.Empty;
    }
}


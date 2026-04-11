using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        [Range(0, double.MaxValue, ErrorMessage = "Giá Tour không được âm")]
        public decimal Price { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Thời lượng không được âm")]
        public int Duration { get; set; } // Số ngày
        
        public string Destination { get; set; } = string.Empty;
        
        [Range(0, int.MaxValue, ErrorMessage = "Số khách không được âm")]
        public int MaxParticipants { get; set; }
        
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string SearchKeywords { get; set; } = string.Empty;
    }
}

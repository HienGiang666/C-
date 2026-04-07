using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        [Range(1, double.MaxValue, ErrorMessage = "Giá Tour phải lớn hơn 0")]
        public decimal Price { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "Thời lượng tối thiểu là 1 ngày")]
        public int Duration { get; set; } // Số ngày
        
        public string Destination { get; set; } = string.Empty;
        
        [Range(1, int.MaxValue, ErrorMessage = "Số khách tham gia tối thiểu là 1")]
        public int MaxParticipants { get; set; }
        
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string SearchKeywords { get; set; } = string.Empty;
    }
}

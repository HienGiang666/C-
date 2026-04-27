using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        
        [Range(0, double.MaxValue, ErrorMessage = "Giá Tour không được âm")]
        public decimal Price { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Thời lượng không được âm")]
        public int Duration { get; set; } = 1; // Số ngày
        
        public string? Destination { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Số khách không được âm")]
        public int MaxParticipants { get; set; }
        
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string? SearchKeywords { get; set; }

        /// <summary>Mã nghiệp vụ TR-1, TR-2... (Business Key, VARCHAR).</summary>
        public string? Code { get; set; }
        
        public virtual ICollection<TourTranslation> Translations { get; set; } = new List<TourTranslation>();

        // Navigation property for many-to-many with POIs
        public virtual ICollection<TourPOI> TourPOIs { get; set; } = new List<TourPOI>();
    }
}

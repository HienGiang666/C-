namespace TourApp.API.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Duration { get; set; } // Số ngày
        public string Destination { get; set; } = string.Empty;
        public int MaxParticipants { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string SearchKeywords { get; set; } = string.Empty;
    }
}

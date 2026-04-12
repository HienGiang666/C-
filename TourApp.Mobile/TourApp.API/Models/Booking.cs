using System.ComponentModel.DataAnnotations;

namespace TourApp.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        public int UserId { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "S? lu?ng kh�ch ph?i l?n hon 0")]
        public int NumberOfParticipants { get; set; }
        
        public DateTime BookingDate { get; set; } = DateTime.Now;
        public DateTime TourDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled
        public string Notes { get; set; } = string.Empty;

        /// <summary>M� nghi?p v? BK-n (l?u DB; FK TourId/UserId v?n tr? Id n?i b?).</summary>
        public int PublicCatalogNumber { get; set; }
    }
}

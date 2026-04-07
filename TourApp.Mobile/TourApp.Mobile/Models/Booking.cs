using System.ComponentModel.DataAnnotations;

namespace TourApp.Mobile.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        public int UserId { get; set; }
        
        [Range(1, int.MaxValue, ErrorMessage = "S? lu?ng khách ph?i l?n hon 0")]
        public int NumberOfParticipants { get; set; }
        
        public DateTime BookingDate { get; set; } = DateTime.Now;
        public DateTime TourDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled
        public string Notes { get; set; } = string.Empty;
    }
}


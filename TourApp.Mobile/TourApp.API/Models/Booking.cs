namespace TourApp.API.Models
{
    public class Booking
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        public int UserId { get; set; }
        public int NumberOfParticipants { get; set; }
        public DateTime BookingDate { get; set; } = DateTime.Now;
        public DateTime TourDate { get; set; }
        public decimal TotalPrice { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Confirmed, Cancelled
        public string Notes { get; set; } = string.Empty;
    }
}

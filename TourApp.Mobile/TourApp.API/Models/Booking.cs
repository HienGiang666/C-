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

        /// <summary>Mã nghiệp vụ BK-1, BK-2... (Business Key, VARCHAR).</summary>
        public string? Code { get; set; }

        // Payment fields (added in Migration v2)
        public string? PaymentMethod { get; set; }      // QR, Momo, CreditCard...
        public string? TransactionId { get; set; }      // Mã giao dịch giả lập
        public DateTime? PaidAt { get; set; }           // Thời gian thanh toán thành công
        public DateTime? CancelledAt { get; set; }      // Thời gian hủy
        public string? CancelReason { get; set; }         // Lý do hủy

        // Guest booking fields (no login required)
        public string? GuestName { get; set; }
        public string? GuestPhone { get; set; }

        // Navigation properties
        public Tour? Tour { get; set; }
        public User? User { get; set; }
        public ICollection<Payment>? Payments { get; set; } = new List<Payment>();
    }
}

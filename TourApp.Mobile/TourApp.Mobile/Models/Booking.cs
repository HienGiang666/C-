using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class Booking
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("tourId")]
        public int TourId { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("numberOfParticipants")]
        public int NumberOfParticipants { get; set; } = 1;

        [JsonPropertyName("bookingDate")]
        public DateTime BookingDate { get; set; } = DateTime.Now;

        [JsonPropertyName("tourDate")]
        public DateTime TourDate { get; set; }

        [JsonPropertyName("totalPrice")]
        public decimal TotalPrice { get; set; } = 0;

        [JsonPropertyName("status")]
        public string? Status { get; set; } = "Pending";

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("code")]
        public string? Code { get; set; }
        
        // Payment fields (added in Migration v2)
        [JsonPropertyName("paymentMethod")]
        public string? PaymentMethod { get; set; }
        
        [JsonPropertyName("transactionId")]
        public string? TransactionId { get; set; }
        
        [JsonPropertyName("paidAt")]
        public DateTime? PaidAt { get; set; }
        
        [JsonPropertyName("cancelledAt")]
        public DateTime? CancelledAt { get; set; }
        
        [JsonPropertyName("cancelReason")]
        public string? CancelReason { get; set; }
        
        // Guest booking fields (no login required)
        [JsonPropertyName("guestName")]
        public string? GuestName { get; set; }

        [JsonPropertyName("guestPhone")]
        public string? GuestPhone { get; set; }

        // Dùng riêng cho UI Mobile để hiển thị tên Tour
        [JsonIgnore]
        public string? TourName { get; set; }
        
        // Helper property to check if booking is paid
        [JsonIgnore]
        public bool IsPaid => Status?.Equals("Paid", StringComparison.OrdinalIgnoreCase) == true || PaidAt != null;
    }
}

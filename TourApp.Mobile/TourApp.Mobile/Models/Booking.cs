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
        
        // Dùng riêng cho UI Mobile để hiển thị tên Tour
        [JsonIgnore]
        public string? TourName { get; set; }
    }
}

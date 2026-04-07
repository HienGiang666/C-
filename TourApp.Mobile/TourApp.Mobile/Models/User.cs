using System.Text.Json.Serialization;

namespace TourApp.Mobile.Models
{
    public class User
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("fullName")]
        public string? FullName { get; set; }

        [JsonPropertyName("username")]
        public string? Username { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("phoneNumber")]
        public string? PhoneNumber { get; set; }

        [JsonPropertyName("address")]
        public string? Address { get; set; }

        [JsonPropertyName("dateOfBirth")]
        public DateTime? DateOfBirth { get; set; }

        [JsonPropertyName("role")]
        public string? Role { get; set; }

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime? CreatedAt { get; set; }

        [JsonPropertyName("lastLoginAt")]
        public DateTime? LastLoginAt { get; set; }

        // Computed property for display
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Username ?? "Người dùng";
    }

    public class Booking
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("userId")]
        public int UserId { get; set; }

        [JsonPropertyName("tourId")]
        public int TourId { get; set; }

        [JsonPropertyName("bookingDate")]
        public DateTime? BookingDate { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; } // Confirmed, Completed, Cancelled

        [JsonPropertyName("notes")]
        public string? Notes { get; set; }

        [JsonPropertyName("participants")]
        public int? Participants { get; set; }

        [JsonPropertyName("totalPrice")]
        public decimal? TotalPrice { get; set; }

        // Tour name from API (stored in Notes temporarily)
        public string? TourName => Notes;
    }
}

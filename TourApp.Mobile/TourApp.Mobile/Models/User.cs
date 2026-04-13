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

        [JsonPropertyName("code")]
        public string? Code { get; set; }

        // Computed property for display
        public string DisplayName => !string.IsNullOrEmpty(FullName) ? FullName : Username ?? "Người dùng";
    }
}

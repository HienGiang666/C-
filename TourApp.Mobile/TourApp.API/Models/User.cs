namespace TourApp.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime DateOfBirth { get; set; }
        public string? Role { get; set; } = "Customer"; // Customer, Admin, RestaurantOwner
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Mã nghiệp vụ #U1, #U2... (Business Key, VARCHAR).</summary>
        public string? Code { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime? LastLoginAt { get; set; }
    }
}

namespace TourApp.API.Models
{
    public class User
    {
        public int Id { get; set; }
        public string? FullName { get; set; }
        public string? Username { get; set; }
        public string? PasswordHash { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Address { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? Role { get; set; } = "Customer"; // Customer, Admin, RestaurantOwner
        public bool IsActive { get; set; } = true;
        public DateTime? CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Mã nghiệp vụ #U1, #U2... (Business Key, VARCHAR).</summary>
        public string? Code { get; set; }

        /// <summary>Thời điểm đăng nhập gần nhất.</summary>
        public DateTime? LastLoginAt { get; set; }
    }
}

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
        public string Role { get; set; } = "Customer"; // Customer, Admin, RestaurantOwner
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        /// <summary>Mã hiển thị cố định #U1, #U2...</summary>
        public int PublicCatalogNumber { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public DateTime? LastLoginAt { get; set; }
    }
}

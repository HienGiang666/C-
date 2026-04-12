using TourApp.API.Models;
using System.Linq;
using TourApp.API.Helpers;

namespace TourApp.API.Data
{
    public static class DbSeeder
    {
        // Hệ thống đã được chuyển sang dùng SQL Script gốc để khởi tạo Database.
  

        public static void ApplySchemaPatches(AppDbContext context) { }

        public static void EnsureBusinessKeyCodes(AppDbContext context) { }

        public static void Seed(AppDbContext context) 
        { 
            Console.WriteLine("[DbSeeder] Synchronizing Admin account...");
            var adminHash = SecurityHelper.HashPassword("admin123");

            // CHỈ đồng bộ mật khẩu cho Admin chính để đảm bảo luôn vào được
            var admin = context.Users.FirstOrDefault(u => u.Username.ToLower() == "admin");
            if (admin != null)
            {
                admin.PasswordHash = adminHash;
                admin.IsActive = true;
                admin.Role = "Admin";
                Console.WriteLine("[DbSeeder] Main Admin updated to 'admin123'.");
            }
            else
            {
                context.Users.Add(new User
                {
                    FullName = "Quản trị viên",
                    Username = "admin",
                    PasswordHash = adminHash,
                    Email = "admin@tourapp.vn",
                    Role = "Admin",
                    IsActive = true,
                    Code = "#U1001",
                    CreatedAt = DateTime.Now
                });
                Console.WriteLine("[DbSeeder] Main Admin created with 'admin123'.");
            }

            // Đồng bộ mật khẩu cho các tài khoản đặc biệt: Pham, cuongpham, hien
            var specialUsernames = new[] { "pham", "cuongpham", "hien" };
            var specialUsers = context.Users.Where(u => specialUsernames.Contains(u.Username.ToLower())).ToList();
            foreach (var user in specialUsers)
            {
                user.PasswordHash = adminHash; // Set mật khẩu mặc định là 'admin123' cho các user này
                user.IsActive = true;
                Console.WriteLine($"[DbSeeder] Special User '{user.Username}' password updated to 'admin123'.");
            }

            // Đối với các user khác, chúng ta GIỮ NGUYÊN mật khẩu họ đã đổi trước đó
            // Chỉ đảm bảo Username không bị trống
            var otherUsers = context.Users.Where(u => u.Username != "admin").ToList();
            foreach (var user in otherUsers)
            {
                if (string.IsNullOrWhiteSpace(user.Username)) {
                    user.Username = user.FullName?.Replace(" ", "").ToLower() ?? "user" + user.Id;
                }
            }

            context.SaveChanges();
            Console.WriteLine("[DbSeeder] Synchronization complete.");
        }

        public static void AssignPoiOwnersCuongHien(AppDbContext context) { }
    }
}
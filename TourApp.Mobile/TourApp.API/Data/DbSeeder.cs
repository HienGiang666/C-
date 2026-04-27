using TourApp.API.Models;
using System.Linq;
using TourApp.API.Helpers;
using Microsoft.EntityFrameworkCore;

namespace TourApp.API.Data
{
    public static class DbSeeder
    {
        // Hệ thống đã được chuyển sang dùng SQL Script gốc để khởi tạo Database.

  

        public static void ApplySchemaPatches(AppDbContext context)
        {
            try
            {
                // Thêm cột IsMock nếu chưa có (tương thích PostgreSQL & SQL Server)
                var sql = context.Database.ProviderName?.Contains("PostgreSQL") == true
                    ? "ALTER TABLE \"UserLocationLogs\" ADD COLUMN IF NOT EXISTS \"IsMock\" boolean NOT NULL DEFAULT false;"
                    : "IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[UserLocationLogs]') AND name = 'IsMock') ALTER TABLE [UserLocationLogs] ADD [IsMock] bit NOT NULL DEFAULT 0;";
                context.Database.ExecuteSqlRaw(sql);
                Console.WriteLine("[SchemaPatch] IsMock column ensured on UserLocationLogs.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SchemaPatch] {ex.Message}");
            }
        }

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

            // 2. Đồng bộ mật khẩu cho các tài khoản đặc biệt (Dùng '12345678')
            var specialPassHash = SecurityHelper.HashPassword("12345678");
            
            var specialUsernames = new[] { "pham", "cuongpham", "hien", "cuong", "cuongowner" };
            foreach (var uname in specialUsernames)
            {
                var u = context.Users.FirstOrDefault(x => x.Username.ToLower() == uname);
                if (u != null) { u.PasswordHash = specialPassHash; u.IsActive = true; }
            }

            Console.WriteLine("[DbSeeder] Reset passwords for special accounts to '12345678'.");

            // 3. Đối với các user khác, chúng ta GIỮ NGUYÊN mật khẩu họ đã đổi trước đó (không ghi đè)
            var otherUsers = context.Users
                .Where(u => u.Username.ToLower() != "admin" && !specialUsernames.Contains(u.Username.ToLower()))
                .ToList();
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
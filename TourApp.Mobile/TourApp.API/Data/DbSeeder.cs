using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TourApp.API.Models;

namespace TourApp.API.Data
{
    public static class DbSeeder
    {
        public static void ApplySchemaPatches(AppDbContext context)
        {
            if (!context.Database.IsSqlServer())
                return;

            try
            {
                context.Database.ExecuteSqlRaw("""
                    IF COL_LENGTH(OBJECT_ID(N'dbo.POIs', N'U'), N'ApprovalStatus') IS NULL
                    BEGIN
                        ALTER TABLE dbo.POIs ADD ApprovalStatus nvarchar(40) NOT NULL
                            CONSTRAINT DF_POIs_ApprovalStatus DEFAULT N'Approved';
                    END
                    IF COL_LENGTH(OBJECT_ID(N'dbo.POIs', N'U'), N'OwnerUserId') IS NULL
                    BEGIN
                        ALTER TABLE dbo.POIs ADD OwnerUserId int NULL;
                    END
                    """);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApplySchemaPatches] POIs (ApprovalStatus/OwnerUserId): {ex.Message}");
            }

            // Tách từng bảng: một lệnh ALTER lỗi không được chặn cột Users (đăng nhập CMS).
            TryAddCodeColumn(context, "Users");
            TryAddCodeColumn(context, "POIs");
            TryAddCodeColumn(context, "Tours");
            TryAddCodeColumn(context, "Bookings");
        }

        private static void TryAddCodeColumn(AppDbContext context, string table)
        {
            try
            {
                if (!context.Database.IsSqlServer())
                    return;

                var sql = $"""
                    IF COL_LENGTH(OBJECT_ID(N'dbo.{table}', N'U'), N'Code') IS NULL
                    BEGIN
                        ALTER TABLE dbo.{table} ADD Code nvarchar(20) NULL;
                    END
                    """;
                context.Database.ExecuteSqlRaw(sql);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ApplySchemaPatches] dbo.{table}.Code: {ex.Message}");
            }
        }

        /// <summary>Gán Code mặc định cho bản ghi còn NULL (sau khi thêm cột).</summary>
        public static void EnsureBusinessKeyCodes(AppDbContext context)
        {
            try
            {
                // POIs: #P{Id}
                var poiNulls = context.POIs.Where(p => string.IsNullOrEmpty(p.Code)).OrderBy(p => p.Id).ToList();
                if (poiNulls.Count > 0)
                {
                    foreach (var p in poiNulls)
                        p.Code = $"#P{p.Id}";
                    context.SaveChanges();
                }

                // Users: #U{Id}
                var userNulls = context.Users.Where(u => string.IsNullOrEmpty(u.Code)).OrderBy(u => u.Id).ToList();
                if (userNulls.Count > 0)
                {
                    foreach (var u in userNulls)
                        u.Code = $"#U{u.Id}";
                    context.SaveChanges();
                }

                // Tours: TR-{Id}
                var tourNulls = context.Tours.Where(t => string.IsNullOrEmpty(t.Code)).OrderBy(t => t.Id).ToList();
                if (tourNulls.Count > 0)
                {
                    foreach (var t in tourNulls)
                        t.Code = $"TR-{t.Id}";
                    context.SaveChanges();
                }

                // Bookings: BK-{Id}
                var bookingNulls = context.Bookings.Where(b => string.IsNullOrEmpty(b.Code)).OrderBy(b => b.Id).ToList();
                if (bookingNulls.Count > 0)
                {
                    foreach (var b in bookingNulls)
                        b.Code = $"BK-{b.Id}";
                    context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EnsureBusinessKeyCodes] {ex.Message}");
            }
        }

        public static void Seed(AppDbContext context)
        {
            // ============================================================
            //  CHỈ SEED KHI TABLE TRỐNG — không xóa data cũ mỗi lần start
            //  Lý do trước đây crash: xóa cả bảng (RemoveRange) khi đang có
            //  FK constraint giữa các bảng → SqlException "FK violation".
            //  DBCC CHECKIDENT RESEED cũng gây race condition khi VS debugger
            //  đang attach và eval assembly → VMDisconnectedException.
            // ============================================================
            if (context.Users.Any()) return; // Đã có data → bỏ qua toàn bộ

            // ============================================================
            //  1. SEED USERS (Admin + 2 khách hàng mẫu)
            // ============================================================
            var admin = new User
            {
                Username = "admin",
                PasswordHash = HashPassword("admin123"),
                FullName = "Quản trị viên",
                Email = "admin@tourapp.vn",
                PhoneNumber = "0901234567",
                Address = "Quận 4, TP.HCM",
                DateOfBirth = new DateTime(1990, 1, 1),
                Role = "Admin",
                IsActive = true,
                CreatedAt = DateTime.Now,
                Code = "#U1001"
            };
            var user1 = new User
            {
                Username = "nguyenvan",
                PasswordHash = HashPassword("123456"),
                FullName = "Nguyễn Văn An",
                Email = "nguyenan@gmail.com",
                PhoneNumber = "0912345678",
                Address = "Quận 1, TP.HCM",
                DateOfBirth = new DateTime(1995, 6, 15),
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-30),
                Code = "#U1002"
            };
            var user2 = new User
            {
                Username = "tranthib",
                PasswordHash = HashPassword("123456"),
                FullName = "Trần Thị Bình",
                Email = "tranbinhh@gmail.com",
                PhoneNumber = "0923456789",
                Address = "Quận 7, TP.HCM",
                DateOfBirth = new DateTime(1998, 3, 20),
                Role = "Customer",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-15),
                Code = "#U1003"
            };
            context.Users.AddRange(admin, user1, user2);
            context.SaveChanges();

            // ============================================================
            //  2. SEED 10 POIs (Quận 4 - Ẩm thực đường phố)
            // ============================================================
            var poi1  = new POI { Name = "Ốc Oanh",            Description = "Hải sản tươi sống, nêm nếm đậm đà, nổi tiếng nhất con đường Vĩnh Khánh.",  Latitude = 10.759902, Longitude = 106.701834, Address = "534 Vĩnh Khánh, Q4",    OpenTime = "15:00 - 23:00", Priority = 1,  Radius = 80,  Rating = 4.6, IsActive = true, Code = "#P1001" };
            var poi2  = new POI { Name = "Phá Lấu Bò Cô Oanh", Description = "Phá lấu nước cốt dừa thơm lừng, ăn kèm bánh mì nóng giòn.",                Latitude = 10.762145, Longitude = 106.704251, Address = "200/20 Xóm Chiếu, Q4", OpenTime = "14:00 - 22:00", Priority = 2,  Radius = 80,  Rating = 4.7, IsActive = true, Code = "#P1002" };
            var poi3  = new POI { Name = "Mì Ốc Hến Dì Lan",   Description = "Mì ốc hến chua cay siêu ngon, hủ tiếu nước trong veo.",                    Latitude = 10.761011, Longitude = 106.704838, Address = "2/4 Ngô Văn Sở, Q4",   OpenTime = "07:00 - 19:00", Priority = 3,  Radius = 50,  Rating = 4.4, IsActive = true, Code = "#P1003" };
            var poi4  = new POI { Name = "Bánh Xèo Bà Hai",     Description = "Bánh xèo miền Tây vỏ giòn rụm, nhân tôm thịt đầy ắp, chấm nước mắm ngon.",Latitude = 10.760195, Longitude = 106.708298, Address = "119 Tôn Đản, Q4",      OpenTime = "15:00 - 22:00", Priority = 4,  Radius = 100, Rating = 4.5, IsActive = true, Code = "#P1004" };
            var poi5  = new POI { Name = "Súp Cua Hằng",        Description = "Súp cua sánh đặc, trứng bắc thảo, óc heo – món ăn 'biểu tượng' của Q4.", Latitude = 10.762510, Longitude = 106.704880, Address = "C200 Xóm Chiếu, Q4",  OpenTime = "16:00 - 23:00", Priority = 5,  Radius = 80,  Rating = 4.8, IsActive = true, Code = "#P1005" };
            var poi6  = new POI { Name = "Cơm Tấm Bãi Rác",     Description = "Sườn cốt lết to nướng than hoa thơm lừng, ăn với bì chả ngon.",           Latitude = 10.763150, Longitude = 106.704952, Address = "73 Lê Quốc Hưng, Q4",   OpenTime = "17:00 - 03:00", Priority = 6,  Radius = 80,  Rating = 4.2, IsActive = true, Code = "#P1006" };
            var poi7  = new POI { Name = "Xôi Mặn Tôn Đản",     Description = "Xôi đầy ắp patê thịt kho, lạp xưởng Tàu và trứng muối.",                 Latitude = 10.758820, Longitude = 106.709350, Address = "240 Tôn Đản, Q4",      OpenTime = "06:00 - 12:00", Priority = 7,  Radius = 70,  Rating = 4.5, IsActive = true, Code = "#P1007" };
            var poi8  = new POI { Name = "Ốc Vũ",               Description = "Các món ốc xào sate cực ngon, chả ốc nhồi, ốc len xào dừa.",             Latitude = 10.759245, Longitude = 106.701104, Address = "37 Vĩnh Khánh, Q4",    OpenTime = "15:00 - 00:00", Priority = 8,  Radius = 120, Rating = 4.3, IsActive = true, Code = "#P1008" };
            var poi9  = new POI { Name = "Chè Cung Đình Huế",   Description = "Hơn 20 loại chè truyền thống xứ Huế, chè đậu ván bánh lọc thơm.",        Latitude = 10.764510, Longitude = 106.705602, Address = "10 Hoàng Diệu, Q4",    OpenTime = "18:00 - 23:00", Priority = 9,  Radius = 50,  Rating = 4.6, IsActive = true, Code = "#P1009" };
            var poi10 = new POI { Name = "Chợ Xóm Chiếu",       Description = "Thiên đường ẩm thực đường phố giá rẻ, hàng trăm gian hàng buổi tối.",    Latitude = 10.761890, Longitude = 106.704200, Address = "Phường 14, Q4",       OpenTime = "15:00 - 22:00", Priority = 10, Radius = 150, Rating = 4.9, IsActive = true, Code = "#P1010" };
            context.POIs.AddRange(poi1, poi2, poi3, poi4, poi5, poi6, poi7, poi8, poi9, poi10);
            context.SaveChanges();

            // ============================================================
            //  3. SEED 3 TOURS
            // ============================================================
            var tour1 = new Tour
            {
                Name = "Tour Ẩm Thực Vĩnh Khánh - Con Đường Ốc",
                Description = "Khám phá con phố ẩm thực nổi tiếng nhất Quận 4 – đường Vĩnh Khánh. Thưởng thức đặc sản ốc, hải sản tươi sống và các món nhậu dân dã.",
                Price = 250000,
                Duration = 1,
                Destination = "Đường Vĩnh Khánh, Quận 4, TP.HCM",
                MaxParticipants = 20,
                SearchKeywords = "ốc vĩnh khánh hải sản quận 4 đêm",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-10),
                Code = "TR-1001"
            };
            var tour2 = new Tour
            {
                Name = "Tour Ẩm Thực Buổi Sáng Quận 4",
                Description = "Hành trình khám phá bữa sáng Quận 4: xôi mặn Tôn Đản, mì ốc hến, hủ tiếu… Những món ăn đậm chất Sài Gòn bình dân.",
                Price = 150000,
                Duration = 1,
                Destination = "Tôn Đản – Xóm Chiếu, Quận 4, TP.HCM",
                MaxParticipants = 15,
                SearchKeywords = "bữa sáng sáng sớm xôi mì hủ tiếu quận 4",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-7),
                Code = "TR-1002"
            };
            var tour3 = new Tour
            {
                Name = "Tour Xóm Chiếu - Chợ Đêm Ẩm Thực",
                Description = "Tham quan chợ Xóm Chiếu – chợ đêm nổi tiếng với hàng trăm món ăn đường phố: phá lấu, súp cua, bánh xèo, chè và nhiều món đặc sắc.",
                Price = 200000,
                Duration = 1,
                Destination = "Khu Xóm Chiếu, Phường 14, Quận 4, TP.HCM",
                MaxParticipants = 25,
                SearchKeywords = "xóm chiếu chợ đêm phá lấu súp cua bánh xèo chè",
                IsActive = true,
                CreatedAt = DateTime.Now.AddDays(-5),
                Code = "TR-1003"
            };
            context.Tours.AddRange(tour1, tour2, tour3);
            context.SaveChanges();

            // ============================================================
            //  4. SEED TourPOIs (mỗi tour có danh sách điểm ghé thăm)
            // ============================================================
            context.TourPOIs.AddRange(
                // Tour 1: Vĩnh Khánh
                new TourPOI { TourId = tour1.Id, POIId = poi1.Id, OrderIndex = 1 },
                new TourPOI { TourId = tour1.Id, POIId = poi8.Id, OrderIndex = 2 },
                new TourPOI { TourId = tour1.Id, POIId = poi5.Id, OrderIndex = 3 },
                // Tour 2: Buổi sáng
                new TourPOI { TourId = tour2.Id, POIId = poi7.Id, OrderIndex = 1 },
                new TourPOI { TourId = tour2.Id, POIId = poi3.Id, OrderIndex = 2 },
                // Tour 3: Xóm Chiếu
                new TourPOI { TourId = tour3.Id, POIId = poi10.Id, OrderIndex = 1 },
                new TourPOI { TourId = tour3.Id, POIId = poi2.Id,  OrderIndex = 2 },
                new TourPOI { TourId = tour3.Id, POIId = poi5.Id,  OrderIndex = 3 },
                new TourPOI { TourId = tour3.Id, POIId = poi4.Id,  OrderIndex = 4 },
                new TourPOI { TourId = tour3.Id, POIId = poi9.Id,  OrderIndex = 5 }
            );
            context.SaveChanges();

            // ============================================================
            //  5. SEED BOOKINGS (dữ liệu mẫu đặt tour)
            // ============================================================
            context.Bookings.AddRange(
                new Booking
                {
                    TourId = tour1.Id, UserId = user1.Id,
                    NumberOfParticipants = 2,
                    BookingDate = DateTime.Now.AddDays(-5),
                    TourDate = DateTime.Now.AddDays(3),
                    TotalPrice = 500000,
                    Status = "Confirmed",
                    Notes = "Dị ứng hải sản nhẹ, vui lòng lưu ý."
                },
                new Booking
                {
                    TourId = tour3.Id, UserId = user2.Id,
                    NumberOfParticipants = 4,
                    BookingDate = DateTime.Now.AddDays(-3),
                    TourDate = DateTime.Now.AddDays(7),
                    TotalPrice = 800000,
                    Status = "Pending",
                    Notes = ""
                },
                new Booking
                {
                    TourId = tour2.Id, UserId = user1.Id,
                    NumberOfParticipants = 1,
                    BookingDate = DateTime.Now.AddDays(-10),
                    TourDate = DateTime.Now.AddDays(-2),
                    TotalPrice = 150000,
                    Status = "Confirmed",
                    Notes = "Đi một mình, hướng dẫn tiếng Anh nếu được."
                },
                new Booking
                {
                    TourId = tour3.Id, UserId = user1.Id,
                    NumberOfParticipants = 3,
                    BookingDate = DateTime.Now.AddDays(-1),
                    TourDate = DateTime.Now.AddDays(14),
                    TotalPrice = 600000,
                    Status = "Cancelled",
                    Notes = "Bận đột xuất, hủy tour."
                }
            );
            context.SaveChanges();
        }

        /// <summary>
        /// Gán chủ quán: POI #P1–#P5 → user Cuong/Cường, #P6–#P10 → Hien/Hiền (Role RestaurantOwner hoặc Staff).
        /// Chạy mỗi lần khởi động API; cần đủ 2 tài khoản trong bảng Users.
        /// </summary>
        public static void AssignPoiOwnersCuongHien(AppDbContext context)
        {
            try
            {
                if (!context.POIs.Any() || !context.Users.Any())
                    return;

                // Lấy tất cả users rồi filter client-side để tránh lỗi SQL NULL
                var allUsers = context.Users.ToList();
                var ownerUsers = allUsers
                    .Where(u => !string.IsNullOrEmpty(u.Role) && 
                                new[] { "restaurantowner", "staff" }.Contains(u.Role.ToLower()))
                    .ToList();

                static bool MatchesOwner(User u, string ascii, string unicode)
                {
                    var un = u.Username ?? string.Empty;
                    if (un.Equals(ascii, StringComparison.OrdinalIgnoreCase))
                        return true;
                    var fn = u.FullName ?? string.Empty;
                    if (fn.Contains(ascii, StringComparison.OrdinalIgnoreCase))
                        return true;
                    return fn.Contains(unicode, StringComparison.Ordinal);
                }

                var cuong = ownerUsers.FirstOrDefault(u => MatchesOwner(u, "Cuong", "Cường"));
                var hien = ownerUsers.FirstOrDefault(u =>
                    u.Id != cuong?.Id && MatchesOwner(u, "Hien", "Hiền"));

                if (cuong == null || hien == null)
                {
                    Console.WriteLine(
                        $"[AssignPoiOwners] Thiếu chủ quán trong DB (Role RestaurantOwner): Cuong/Cường={cuong != null}, Hien/Hiền={hien != null}.");
                    return;
                }

                foreach (var p in context.POIs.ToList())
                {
                    // Gán owner theo Code: #P1001-#P1005 -> Cuong, #P1006-#P1010 -> Hien
                    var codeNum = 0;
                    if (!string.IsNullOrEmpty(p.Code) && p.Code.StartsWith("#P") && int.TryParse(p.Code.Substring(2), out codeNum))
                    {
                        if (codeNum >= 1001 && codeNum <= 1005)
                            p.OwnerUserId = cuong.Id;
                        else if (codeNum >= 1006 && codeNum <= 1010)
                            p.OwnerUserId = hien.Id;
                    }
                }

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AssignPoiOwners] {ex.Message}");
            }
        }

        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }
    }
}

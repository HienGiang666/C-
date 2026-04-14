using Microsoft.EntityFrameworkCore;
using TourApp.API.Models;

namespace TourApp.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // 10 CORE TABLES - Đồng bộ với FullDatabase_Create_With_Data.sql
    public DbSet<User> Users { get; set; }           // 1. Users
    public DbSet<POI> POIs { get; set; }             // 2. POIs
    public DbSet<Tour> Tours { get; set; }            // 3. Tours
    public DbSet<TourPOI> TourPOIs { get; set; }      // 4. TourPOIs (many-to-many)
    public DbSet<Audio> Audios { get; set; }          // 5. Audios
    public DbSet<Booking> Bookings { get; set; }     // 6. Bookings
    public DbSet<FavoritePOI> FavoritePOIs { get; set; } // 7. FavoritePOIs
    public DbSet<UserLocationLog> UserLocationLogs { get; set; } // 8. UserLocationLogs
    public DbSet<NarrationLog> NarrationLogs { get; set; } // 9. NarrationLogs
    public DbSet<POITranslation> POITranslations { get; set; } // 10. POITranslations
    // Note: ActivityLogs không có trong DB, CMS dùng in-memory

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình quan hệ nhiều-nhiều cho Tour và POI
        modelBuilder.Entity<TourPOI>()
            .HasOne(tp => tp.Tour)
            .WithMany(t => t.TourPOIs)
            .HasForeignKey(tp => tp.TourId);

        modelBuilder.Entity<TourPOI>()
            .HasOne(tp => tp.POI)
            .WithMany(p => p.TourPOIs)
            .HasForeignKey(tp => tp.POIId);

        // Cấu hình các ràng buộc Unique nếu cần (giống SQL)
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
    }
}
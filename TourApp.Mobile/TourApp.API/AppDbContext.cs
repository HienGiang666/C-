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
    public DbSet<PopularRoute> PopularRoutes { get; set; } // 8b. PopularRoutes (chỉ đường phổ biến)
    public DbSet<NarrationLog> NarrationLogs { get; set; } // 9. NarrationLogs
    public DbSet<POITranslation> POITranslations { get; set; } // 10. POITranslations
    public DbSet<TourTranslation> TourTranslations { get; set; } // 11. TourTranslations
    
    // NEW: Migration v2 - Payment & Category
    public DbSet<Payment> Payments { get; set; } = null!;          // 12. Payments
    public DbSet<Category> Categories { get; set; } = null!;     // 13. Categories  
    public DbSet<POICategory> POICategories { get; set; } = null!; // 14. POICategories
    
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
        modelBuilder.Entity<TourTranslation>()
            .HasOne(tt => tt.Tour)
            .WithMany(t => t.Translations)
            .HasForeignKey(tt => tt.TourId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();

        // NEW: Migration v2 configurations
        
        // Payment - Indexes
        modelBuilder.Entity<Payment>().HasIndex(p => p.BookingId);
        modelBuilder.Entity<Payment>().HasIndex(p => p.UserId);
        modelBuilder.Entity<Payment>().HasIndex(p => p.Status);

        // POICategory - Composite unique constraint
        modelBuilder.Entity<POICategory>()
            .HasIndex(pc => new { pc.POIId, pc.CategoryId })
            .IsUnique();

        modelBuilder.Entity<POICategory>().HasIndex(pc => pc.CategoryId);

        // POICategory relationships
        modelBuilder.Entity<POICategory>()
            .HasOne(pc => pc.POI)
            .WithMany(p => p.POICategories)
            .HasForeignKey(pc => pc.POIId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<POICategory>()
            .HasOne(pc => pc.Category)
            .WithMany(c => c.POICategories)
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        // Payment relationships
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Booking)
            .WithMany(b => b.Payments)
            .HasForeignKey(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.User)
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
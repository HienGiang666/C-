using Microsoft.EntityFrameworkCore;
using TourApp.API.Models;

namespace TourApp.API.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<POI> POIs { get; set; }
    public DbSet<Tour> Tours { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Audio> Audios { get; set; }
    public DbSet<NarrationLog> NarrationLogs { get; set; }
    public DbSet<UserLocationLog> UserLocationLogs { get; set; }
    public DbSet<TourPOI> TourPOIs { get; set; }
    public DbSet<FavoritePOI> FavoritePOIs { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TourAppDB;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True");
        }
    }
}
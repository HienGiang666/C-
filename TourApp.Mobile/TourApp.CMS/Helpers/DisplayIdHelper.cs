using TourApp.CMS.Models;

namespace TourApp.CMS.Helpers;

/// <summary>Mã hiển thị khớp cột PublicCatalogNumber trong DB (User/Tour/Booking).</summary>
public static class DisplayIdHelper
{
    public static int UserCatalogNumber(User u) =>
        u.PublicCatalogNumber > 0 ? u.PublicCatalogNumber : u.Id;

    public static string UserBadge(User u) => $"#U{UserCatalogNumber(u)}";

    public static string UserBadgeFromNumber(int displayNumber) => $"#U{displayNumber}";

    /// <summary>VD: Cuong(#U5) — dùng cột chủ sở hữu POI / phê duyệt.</summary>
    public static string OwnerDisplayLabel(User u) => $"{u.FullName}({UserBadge(u)})";

    public static int TourCatalogNumber(Tour t) =>
        t.PublicCatalogNumber > 0 ? t.PublicCatalogNumber : t.Id;

    public static int BookingCatalogNumber(Booking b) =>
        b.PublicCatalogNumber > 0 ? b.PublicCatalogNumber : b.Id;

    public static string TourRef(int publicCatalogNumber) => $"TR-{publicCatalogNumber}";

    public static string BookingRef(int publicCatalogNumber) => $"BK-{publicCatalogNumber}";
}

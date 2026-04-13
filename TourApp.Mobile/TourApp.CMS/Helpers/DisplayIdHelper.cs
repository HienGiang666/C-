using TourApp.CMS.Models;

namespace TourApp.CMS.Helpers;

/// <summary>Helper để lấy mã hiển thị từ cột Code (Business Key).</summary>
public static class DisplayIdHelper
{
    // User helpers
    public static string UserBadge(User u) => u.DisplayCode;

    public static string UserBadgeFromCode(string code) => string.IsNullOrEmpty(code) ? "#U?" : code;

    /// <summary>VD: Cuong(#U5) — dùng cột chủ sở hữu POI / phê duyệt.</summary>
    public static string OwnerDisplayLabel(User u) => $"{u.FullName}({UserBadge(u)})";

    // Tour helpers
    public static string TourRef(Tour t) => t.DisplayCode;

    // Booking helpers
    public static string BookingRef(Booking b) => b.DisplayCode;

    // Legacy helpers cho backward compatibility
    public static string TourRef(int id) => $"TR-{id}";
    public static string BookingRef(int id) => $"BK-{id}";
}

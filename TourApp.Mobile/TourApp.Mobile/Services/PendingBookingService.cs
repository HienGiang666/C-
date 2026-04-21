using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services;

/// <summary>
/// Lưu trữ thông tin booking đang dang dở khi user chưa đăng nhập
/// Sau khi đăng nhập sẽ tiếp tục booking
/// </summary>
public static class PendingBookingService
{
    public static PendingBookingInfo? PendingBooking { get; set; }

    public static void Save(int tourId, int participants, DateTime tourDate, string? notes, decimal totalPrice)
    {
        PendingBooking = new PendingBookingInfo
        {
            TourId = tourId,
            Participants = participants,
            TourDate = tourDate,
            Notes = notes,
            TotalPrice = totalPrice,
            SavedAt = DateTime.Now
        };
    }

    public static void Clear()
    {
        PendingBooking = null;
    }

    public static bool HasPendingBooking()
    {
        // Chỉ hợp lệ trong vòng 30 phút
        if (PendingBooking == null) return false;
        return (DateTime.Now - PendingBooking.SavedAt).TotalMinutes <= 30;
    }
}

public class PendingBookingInfo
{
    public int TourId { get; set; }
    public int Participants { get; set; }
    public DateTime TourDate { get; set; }
    public string? Notes { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime SavedAt { get; set; }
}

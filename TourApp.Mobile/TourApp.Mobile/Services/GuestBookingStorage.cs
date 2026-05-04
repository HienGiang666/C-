using System.Diagnostics;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services;

/// <summary>
/// Lưu trữ lịch sử đặt tour của guest ngay trên thiết bị (không cần đăng nhập).
/// Dữ liệu được ghi vào file JSON trong AppDataDirectory.
/// </summary>
public static class GuestBookingStorage
{
    private static readonly string FilePath = Path.Combine(
        FileSystem.AppDataDirectory, "guest_bookings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    /// <summary>Lưu một booking mới vào local.</summary>
    public static void SaveBooking(Booking booking)
    {
        try
        {
            var list = LoadAllRaw();
            list.Add(booking);
            var json = JsonSerializer.Serialize(list, JsonOpts);
            File.WriteAllText(FilePath, json);
            Debug.WriteLine($"[GuestBookingStorage] Saved booking #{booking.Id} to local");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuestBookingStorage] Save error: {ex.Message}");
        }
    }

    /// <summary>Cập nhật trạng thái booking trong local (ví dụ sau thanh toán).</summary>
    public static void UpdateStatus(int bookingId, string status, string? transactionId = null)
    {
        try
        {
            var list = LoadAllRaw();
            var item = list.FirstOrDefault(b => b.Id == bookingId);
            if (item == null) return;
            item.Status = status;
            if (transactionId != null)
                item.TransactionId = transactionId;
            var json = JsonSerializer.Serialize(list, JsonOpts);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuestBookingStorage] Update error: {ex.Message}");
        }
    }

    /// <summary>Đọc toàn bộ lịch sử từ local.</summary>
    public static List<Booking> LoadAll()
    {
        return LoadAllRaw();
    }

    /// <summary>Xóa toàn bộ lịch sử local.</summary>
    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath))
                File.Delete(FilePath);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuestBookingStorage] Clear error: {ex.Message}");
        }
    }

    private static List<Booking> LoadAllRaw()
    {
        try
        {
            if (!File.Exists(FilePath)) return new List<Booking>();
            var json = File.ReadAllText(FilePath);
            var list = JsonSerializer.Deserialize<List<Booking>>(json, JsonOpts);
            return list ?? new List<Booking>();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[GuestBookingStorage] Load error: {ex.Message}");
            return new List<Booking>();
        }
    }
}

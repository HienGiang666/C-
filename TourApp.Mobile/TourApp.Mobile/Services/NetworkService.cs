using System.Diagnostics;

namespace TourApp.Mobile.Services;

/// <summary>
/// Quản lý trạng thái mạng — dùng Connectivity.Current
/// Cung cấp property IsConnected, event ConnectivityChanged, và banner UI helper.
/// </summary>
public static class NetworkService
{
    private static bool _initialized;

    /// <summary>Trạng thái mạng hiện tại</summary>
    public static bool IsConnected =>
        Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    /// <summary>Fired khi trạng thái mạng thay đổi (true = online, false = offline)</summary>
    public static event Action<bool>? ConnectivityChanged;

    /// <summary>Gọi 1 lần khi app khởi động</summary>
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
        Debug.WriteLine($"[NetworkService] Initialized — online={IsConnected}");

        // Sync ngay nếu đang online
        if (IsConnected)
        {
            _ = Task.Run(async () =>
            {
                try { await OfflineQueueService.SyncAllAsync(); }
                catch (Exception ex) { Debug.WriteLine($"[NetworkService] Initial sync error: {ex.Message}"); }
            });
        }
    }

    public static void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
        _initialized = false;
    }

    private static void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var online = e.NetworkAccess == NetworkAccess.Internet;
        Debug.WriteLine($"[NetworkService] Connectivity changed — online={online}");
        ConnectivityChanged?.Invoke(online);

        // Khi có mạng lại → sync offline queue
        if (online)
        {
            _ = Task.Run(async () =>
            {
                try { await OfflineQueueService.SyncAllAsync(); }
                catch (Exception ex) { Debug.WriteLine($"[NetworkService] Auto-sync error: {ex.Message}"); }
            });
        }
    }
}

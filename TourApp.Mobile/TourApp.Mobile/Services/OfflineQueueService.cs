using System.Diagnostics;
using System.Text.Json;

namespace TourApp.Mobile.Services;

/// <summary>
/// Queue các thao tác write (booking, rating, narration log...) khi offline.
/// Tự động đồng bộ khi có mạng lại.
/// </summary>
public static class OfflineQueueService
{
    private static readonly string QueueFile =
        Path.Combine(FileSystem.AppDataDirectory, "offline_queue.json");

    private static readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    /// <summary>Fired khi sync hoàn tất (số lượng thành công, số lượng thất bại)</summary>
    public static event Action<int, int>? SyncCompleted;

    // ===== Enqueue =====

    public static async Task EnqueueAsync(OfflineAction action)
    {
        await _lock.WaitAsync();
        try
        {
            var list = await LoadQueueAsync();
            list.Add(action);
            await SaveQueueAsync(list);
            Debug.WriteLine($"[OfflineQueue] Enqueued: {action.Type} (total={list.Count})");
        }
        finally { _lock.Release(); }
    }

    public static async Task<int> GetPendingCountAsync()
    {
        var list = await LoadQueueAsync();
        return list.Count;
    }

    // ===== Sync =====

    public static async Task SyncAllAsync()
    {
        if (!NetworkService.IsConnected) return;

        await _lock.WaitAsync();
        List<OfflineAction> queue;
        try
        {
            queue = await LoadQueueAsync();
            if (queue.Count == 0) return;
        }
        finally { _lock.Release(); }

        Debug.WriteLine($"[OfflineQueue] Syncing {queue.Count} pending actions...");
        int success = 0, fail = 0;
        var remaining = new List<OfflineAction>();

        foreach (var action in queue)
        {
            try
            {
                var ok = await ExecuteActionAsync(action);
                if (ok) success++;
                else { fail++; remaining.Add(action); }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[OfflineQueue] Sync error for {action.Type}: {ex.Message}");
                fail++;
                remaining.Add(action);
            }
        }

        await _lock.WaitAsync();
        try { await SaveQueueAsync(remaining); }
        finally { _lock.Release(); }

        Debug.WriteLine($"[OfflineQueue] Sync done: {success} OK, {fail} failed, {remaining.Count} remaining");
        SyncCompleted?.Invoke(success, fail);
    }

    // ===== Execute =====

    private static async Task<bool> ExecuteActionAsync(OfflineAction action)
    {
        var apiService = new ApiService();
        switch (action.Type)
        {
            case OfflineActionType.Booking:
                var booking = JsonSerializer.Deserialize<Models.Booking>(action.Payload, _jsonOpts);
                if (booking == null) return true; // discard invalid
                var (ok, _) = await apiService.BookTourAsync(booking);
                return ok;

            case OfflineActionType.NarrationLog:
                var log = JsonSerializer.Deserialize<NarrationLogPayload>(action.Payload, _jsonOpts);
                if (log == null) return true;
                await apiService.LogNarrationAsync(log.PoiId, log.AudioId, log.TriggerType, log.DeviceId);
                return true;

            default:
                Debug.WriteLine($"[OfflineQueue] Unknown action type: {action.Type}");
                return true; // discard unknown
        }
    }

    // ===== Persistence =====

    private static async Task<List<OfflineAction>> LoadQueueAsync()
    {
        try
        {
            if (!File.Exists(QueueFile)) return new();
            var json = await File.ReadAllTextAsync(QueueFile);
            return JsonSerializer.Deserialize<List<OfflineAction>>(json, _jsonOpts) ?? new();
        }
        catch
        {
            return new();
        }
    }

    private static async Task SaveQueueAsync(List<OfflineAction> queue)
    {
        var json = JsonSerializer.Serialize(queue, _jsonOpts);
        await File.WriteAllTextAsync(QueueFile, json);
    }
}

// ===== Models =====

public enum OfflineActionType
{
    Booking,
    NarrationLog
}

public class OfflineAction
{
    public OfflineActionType Type { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

public class NarrationLogPayload
{
    public int PoiId { get; set; }
    public int? AudioId { get; set; }
    public string TriggerType { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

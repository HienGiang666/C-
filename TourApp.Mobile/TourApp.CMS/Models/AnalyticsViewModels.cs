namespace TourApp.CMS.Models;

// ViewModels cho Narration Stats
public class NarrationStatsViewModel
{
    public int TotalPlays { get; set; }
    public List<TopPOIViewModel> TopPOIs { get; set; } = new();
}

public class TopPOIViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int Count { get; set; }
}

// Response từ API NarrationLog/stats
public class NarrationStatsResponse
{
    public int total { get; set; }
    public List<TopPOIResponse>? topPoi { get; set; }
}

public class TopPOIResponse
{
    public int poiId { get; set; }
    public string? poiName { get; set; }
    public int count { get; set; }
}

// ViewModels cho UserLocation Stats
public class UserLocationStatsViewModel
{
    public int OnlineNow { get; set; }
    public int Active24h { get; set; }
    public List<OnlineLocationViewModel> OnlineLocations { get; set; } = new();
}

public class OnlineLocationViewModel
{
    public string DeviceId { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
    public string? SessionId { get; set; }
}

// Response từ API UserLocation/stats
public class UserLocationStatsResponse
{
    public int onlineNow { get; set; }
    public int active24h { get; set; }
    public List<OnlineLocationResponse>? onlineLocations { get; set; }
}

public class OnlineLocationResponse
{
    public string deviceId { get; set; } = string.Empty;
    public double latitude { get; set; }
    public double longitude { get; set; }
    public DateTime timestamp { get; set; }
    public string? sessionId { get; set; }
}

// ViewModels cho Duration Stats
public class DurationStatsViewModel
{
    public string GlobalAverageFormatted { get; set; } = "00:00";
    public List<POIDurationViewModel> POIDurations { get; set; } = new();
}

public class POIDurationViewModel
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string AvgDurationFormatted { get; set; } = "00:00";
    public int TotalListens { get; set; }
}

// Response từ API NarrationLog/duration-stats
public class DurationStatsResponse
{
    public double globalAverageSeconds { get; set; }
    public string? globalAverageFormatted { get; set; }
    public List<POIDurationResponse>? poiDurations { get; set; }
}

public class POIDurationResponse
{
    public int poiId { get; set; }
    public string? poiName { get; set; }
    public double avgDurationSeconds { get; set; }
    public string? avgDurationFormatted { get; set; }
    public int totalListens { get; set; }
    public int totalDurationSeconds { get; set; }
}

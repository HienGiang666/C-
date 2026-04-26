#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Maui.Devices.Sensors;

namespace TourApp.Mobile.Platforms.Android.Services;

[Service(
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeLocation,
    Name = "com.companyname.tourapp.mobile.LocationForegroundService"
)]
public class LocationForegroundService : Service
{
    private CancellationTokenSource? _cts;
    private const int NOTIFICATION_ID = 1001;
    private const string CHANNEL_ID = "location_service_channel";
    private const string CHANNEL_NAME = "GPS Tracking";
    private string _statusText = "Đang theo dõi vị trí...";

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Service created");
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] OnStartCommand");

        try
        {
            var notification = CreateNotification(_statusText);
            StartForeground(NOTIFICATION_ID, notification);
            StartLocationTracking();
        }
        catch (Java.Lang.SecurityException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] SecurityException: {ex.Message}");
            // Permission not granted, stop service
            StopSelf();
            return StartCommandResult.NotSticky;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] Error: {ex.Message}");
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        return StartCommandResult.Sticky;
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override void OnDestroy()
    {
        base.OnDestroy();
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Service destroyed");
        StopLocationTracking();
    }

    private void CreateNotificationChannel()
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                CHANNEL_ID,
                CHANNEL_NAME,
                NotificationImportance.Low
            )
            {
                Description = "Kênh thông báo cho dịch vụ theo dõi vị trí nền"
            };

            var notificationManager = GetSystemService(NotificationService) as NotificationManager;
            notificationManager?.CreateNotificationChannel(channel);
        }
    }

    private Notification CreateNotification(string text)
    {
        var builder = new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetContentTitle("Tour Vĩnh Khánh")
            .SetContentText(text)
            .SetSmallIcon(Resource.Drawable.notification_icon_background)
            .SetOngoing(true)
            .SetPriority(NotificationCompat.PriorityLow)
            .SetCategory(NotificationCompat.CategoryService);

        return builder.Build();
    }

    private void UpdateNotification(string text)
    {
        _statusText = text;
        var notification = CreateNotification(text);
        
        var notificationManager = GetSystemService(NotificationService) as NotificationManager;
        notificationManager?.Notify(NOTIFICATION_ID, notification);
        
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            StartForeground(NOTIFICATION_ID, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
        }
        else
        {
            StartForeground(NOTIFICATION_ID, notification);
        }
    }

    private void StartLocationTracking()
    {
        // Foreground service chỉ giữ app alive khi minimize.
        // GPS polling + gửi API được xử lý bởi LocationService.RunGpsLoopSafe()
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Foreground service started (keeps app alive). GPS loop handled by LocationService.");
    }

    private void StopLocationTracking()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <summary>
    /// Start the foreground service
    /// </summary>
    public static void Start(Context context)
    {
        // Check location permission before starting (Android 14+ requirement)
        if ((int)Build.VERSION.SdkInt >= 34) // Android 14+
        {
            var hasLocation = AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context, global::Android.Manifest.Permission.AccessFineLocation) == global::Android.Content.PM.Permission.Granted
                || AndroidX.Core.Content.ContextCompat.CheckSelfPermission(
                context, global::Android.Manifest.Permission.AccessCoarseLocation) == global::Android.Content.PM.Permission.Granted;

            if (!hasLocation)
            {
                System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Cannot start: Location permission not granted");
                return;
            }
        }

        var intent = new Intent(context, typeof(LocationForegroundService));
        
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            context.StartForegroundService(intent);
        }
        else
        {
            context.StartService(intent);
        }
        
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Start requested");
    }

    /// <summary>
    /// Stop the foreground service
    /// </summary>
    public static void Stop(Context context)
    {
        var intent = new Intent(context, typeof(LocationForegroundService));
        context.StopService(intent);
        System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Stop requested");
    }
}
#endif

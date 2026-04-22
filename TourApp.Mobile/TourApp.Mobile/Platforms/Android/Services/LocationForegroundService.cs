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

        var notification = CreateNotification(_statusText);
        
        if (Build.VERSION.SdkInt >= BuildVersionCodes.S)
        {
            StartForeground(NOTIFICATION_ID, notification, global::Android.Content.PM.ForegroundService.TypeLocation);
        }
        else
        {
            StartForeground(NOTIFICATION_ID, notification);
        }

        StartLocationTracking();

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

    private async void StartLocationTracking()
    {
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    var request = new GeolocationRequest(
                        GeolocationAccuracy.Best,
                        TimeSpan.FromSeconds(10)
                    );

                    var location = await Geolocation.Default.GetLocationAsync(request, token);

                    if (location != null)
                    {
                        var msg = $"Vị trí: {location.Latitude:F4}, {location.Longitude:F4}";
                        UpdateNotification(msg);

                        // Broadcast location to the app
                        var intent = new Intent("LOCATION_UPDATE");
                        intent.PutExtra("latitude", location.Latitude);
                        intent.PutExtra("longitude", location.Longitude);
                        SendBroadcast(intent);

                        System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] {msg}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LocationForegroundService] GPS error: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), token);
            }
        }
        catch (System.OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[LocationForegroundService] Tracking cancelled");
        }
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

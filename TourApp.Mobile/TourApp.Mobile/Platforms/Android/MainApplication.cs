using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using TourApp.Mobile.Platforms.Android.Services;

namespace TourApp.Mobile
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();

            // [FIX] Bắt tất cả exception ở tầng Java/Android — đây là loại crash
            // "lặng lẽ" không hiện dialog, app biến mất. Lý do: Mono runtime chuyển
            // unhandled .NET exception sang Java thread → Android kill process mà không báo.
            AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRASH][Android] {args.Exception}");
                args.Handled = true; // Ngăn Android kill process ngay lập tức
            };
        }

        /// <summary>
        /// Check and request notification permission (required for Android 13+)
        /// </summary>
        public static async Task<bool> RequestNotificationPermissionAsync()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.Tiramisu) return true;

            var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
            if (status != PermissionStatus.Granted)
            {
                status = await Permissions.RequestAsync<Permissions.PostNotifications>();
            }
            return status == PermissionStatus.Granted;
        }

        /// <summary>
        /// Start location foreground service
        /// </summary>
        public static void StartLocationService(Context context)
        {
            try
            {
                LocationForegroundService.Start(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainApplication] Failed to start location service: {ex.Message}");
            }
        }

        /// <summary>
        /// Stop location foreground service
        /// </summary>
        public static void StopLocationService(Context context)
        {
            try
            {
                LocationForegroundService.Stop(context);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainApplication] Failed to stop location service: {ex.Message}");
            }
        }
    }
}

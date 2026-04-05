using Android.App;
using Android.Runtime;

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
    }
}

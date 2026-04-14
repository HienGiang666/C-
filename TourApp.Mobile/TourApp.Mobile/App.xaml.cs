using TourApp.Mobile.Services;

namespace TourApp.Mobile
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        public App()
        {
            // ── Global unhandled exception handlers ───────────────────────────
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[CRASH][AppDomain] {ex}");
                // NOTE: AppDomain.UnhandledException là notification-only — app vẫn crash sau đây.
                // Để xem lỗi: check Output > Debug trong VS, hoặc Android logcat.
            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                // [QUAN TRỌNG] SetObserved() ngăn app crash do task exception không được await
                // Đây là catch-all cho mọi _ = Task.Run(...) throw exception
                System.Diagnostics.Debug.WriteLine($"[CRASH][UnobservedTask] {args.Exception}");
                args.SetObserved();
            };
            // ─────────────────────────────────────────────────────────────────

            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            AuthService.LoadSavedSession();

            Page startPage;
            if (AuthService.IsLoggedIn && !string.IsNullOrEmpty(AuthService.CurrentUser?.Username))
            {
                if (Preferences.Default.ContainsKey("app_lock_pin"))
                {
                    startPage = new Views.AppLockPage(0);
                }
                else
                {
                    startPage = new AppShell();
                }
            }
            else
            {
                startPage = new NavigationPage(new Views.Auth.LoginPage());
            }

            return new Window(startPage);
        }
    }
}

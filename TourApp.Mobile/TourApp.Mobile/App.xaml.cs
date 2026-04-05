namespace TourApp.Mobile
{
    public partial class App : Application
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

            // Bắt tất cả .NET unhandled exceptions — log để debug, không crash âm thầm
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRASH][AppDomain] {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[CRASH][Task] {e.Exception?.InnerException?.Message}");
                e.SetObserved(); // Ngăn process bị kill bởi unobserved task exception
            };
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
namespace TourApp.Mobile
{
    public partial class App : Application
    {
        public App()
        {
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
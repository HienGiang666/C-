using TourApp.Mobile.Services;

namespace TourApp.Mobile
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Non-tab page still reached from the authenticated shell.
            Routing.RegisterRoute("QRScannerPage", typeof(Views.QRScannerPage));
            Routing.RegisterRoute("TourDetailPage", typeof(Views.TourDetailPage));
            Routing.RegisterRoute("BookingPage", typeof(Views.BookingPage));
            Routing.RegisterRoute("UserBookingsPage", typeof(Views.UserBookingsPage));
            
            // Subscribe to language changes để update tab labels
            LanguageService.LanguageChanged += OnLanguageChanged;
            
            // Set initial tab labels
            UpdateTabLabels();
        }
        
        private void OnLanguageChanged(object? sender, string newLang)
        {
            MainThread.BeginInvokeOnMainThread(UpdateTabLabels);
        }
        
        private void UpdateTabLabels()
        {
            // Update tab labels theo ngôn ngữ hiện tại
            if (Items.Count > 0 && Items[0] is TabBar tabBar && tabBar.Items.Count >= 5)
            {
                tabBar.Items[0].Title = LanguageService.GetString("TabHome");
                tabBar.Items[1].Title = LanguageService.GetString("TabFood");
                tabBar.Items[2].Title = LanguageService.GetString("TabMap");
                tabBar.Items[3].Title = LanguageService.GetString("TabTour");
                tabBar.Items[4].Title = LanguageService.GetString("TabProfile");
            }
        }
        
        ~AppShell()
        {
            LanguageService.LanguageChanged -= OnLanguageChanged;
        }
    }
}

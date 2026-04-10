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
            if (Items.Count >= 5)
            {
                Items[0].Title = LanguageService.GetString("TabHome");
                Items[1].Title = LanguageService.GetString("TabFood");
                Items[2].Title = LanguageService.GetString("TabMap");
                Items[3].Title = LanguageService.GetString("TabTour");
                Items[4].Title = LanguageService.GetString("TabProfile");
            }
        }
        
        ~AppShell()
        {
            LanguageService.LanguageChanged -= OnLanguageChanged;
        }
    }
}

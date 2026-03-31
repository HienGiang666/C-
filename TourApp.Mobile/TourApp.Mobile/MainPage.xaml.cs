namespace TourApp.Mobile
{
    public partial class MainPage : ContentPage
    {
        private static bool _loginShown = false;

        public MainPage()
        {
            InitializeComponent();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Use a dispatcher timer to show login AFTER page has fully rendered
            // This avoids blocking the main thread during initial layout pass
            if (!_loginShown)
            {
                _loginShown = true;
                Dispatcher.StartTimer(TimeSpan.FromMilliseconds(600), () =>
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Navigation.PushModalAsync(new Views.LoginPage(), false);
                    });
                    return false; // don't repeat
                });
            }
        }

        private async void OnGoToMapClicked(object? sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("MapPageRoute");
        }
    }
}
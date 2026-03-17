namespace TourApp.Mobile
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
        }

        private async void OnTestDBClicked(object sender, EventArgs e)
        {
            var db = new Services.DatabaseService();
            bool ok = await db.TestConnectionAsync();
            await DisplayAlert("Test DB", ok ? "✅ Kết nối thành công!" : "❌ Lỗi kết nối!", "OK");
        }
    }
}
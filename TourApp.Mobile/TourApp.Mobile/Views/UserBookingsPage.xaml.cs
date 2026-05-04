using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class UserBookingsPage : ContentPage
{
    public ObservableCollection<Booking> Bookings { get; set; } = new();

    public UserBookingsPage()
    {
        InitializeComponent();
        BookingsCollectionView.ItemsSource = Bookings;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadBookingsAsync();
    }

    private async Task LoadBookingsAsync()
    {
        MainThread.BeginInvokeOnMainThread(() => BookingsRefreshView.IsRefreshing = true);

        try
        {
            // 1. Luôn load từ local trước (offline-first)
            var localList = GuestBookingStorage.LoadAll();

            // 2. Nếu có mạng, merge với server
            List<Booking>? serverList = null;
            if (NetworkService.IsConnected)
            {
                var apiService = new ApiService();
                if (AuthService.IsGuestMode)
                {
                    var guestPhone = Preferences.Default.Get("guest_phone", "");
                    if (!string.IsNullOrWhiteSpace(guestPhone))
                        serverList = await apiService.GetGuestBookingsAsync(guestPhone);
                }
                else if (AuthService.CurrentUser != null)
                {
                    var authService = new AuthService();
                    serverList = await authService.GetUserBookingsAsync(AuthService.CurrentUser.Id);
                }
            }

            // 3. Merge: server data ưu tiên (có status mới nhất), giữ lại local-only
            var merged = new Dictionary<int, Booking>();
            foreach (var b in localList)
                if (b.Id > 0) merged[b.Id] = b;

            if (serverList != null)
            {
                foreach (var b in serverList)
                    if (b.Id > 0) merged[b.Id] = b;
            }

            var finalList = merged.Values.OrderByDescending(b => b.BookingDate).ToList();

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Bookings.Clear();
                foreach (var b in finalList)
                    Bookings.Add(b);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[UserBookingsPage] error: {ex.Message}");
        }
        finally
        {
            MainThread.BeginInvokeOnMainThread(() => BookingsRefreshView.IsRefreshing = false);
        }
    }

    private void OnRefreshing(object sender, EventArgs e)
    {
        _ = LoadBookingsAsync();
    }

    private async void OnPayBookingClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is Booking booking)
        {
            if (booking.Status == "Pending")
            {
                await Shell.Current.GoToAsync($"PaymentPage?bookingId={booking.Id}");
            }
        }
    }
}

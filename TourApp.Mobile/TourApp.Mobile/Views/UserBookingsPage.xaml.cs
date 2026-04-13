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
        if (AuthService.CurrentUser == null) return;

        MainThread.BeginInvokeOnMainThread(() => BookingsRefreshView.IsRefreshing = true);

        try
        {
            var authService = new AuthService();
            var list = await authService.GetUserBookingsAsync(AuthService.CurrentUser.Id);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                Bookings.Clear();
                if (list != null)
                {
                    foreach (var b in list)
                    {
                        Bookings.Add(b);
                    }
                }
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
}

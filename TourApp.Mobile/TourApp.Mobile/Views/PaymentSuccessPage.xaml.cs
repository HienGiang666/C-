using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

[QueryProperty(nameof(BookingId), "bookingId")]
[QueryProperty(nameof(TransactionId), "transactionId")]
public partial class PaymentSuccessPage : ContentPage
{
    private readonly ApiService _apiService;
    private int _bookingId;
    private string? _transactionId;

    public int BookingId
    {
        get => _bookingId;
        set
        {
            _bookingId = value;
            _ = LoadDataAsync();
        }
    }

    public string? TransactionId
    {
        get => _transactionId;
        set => _transactionId = value;
    }

    public PaymentSuccessPage()
    {
        InitializeComponent();
        _apiService = new ApiService();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var booking = await _apiService.GetBookingAsync(_bookingId);
            if (booking == null) return;

            var tour = await _apiService.GetTourByIdAsync(booking.TourId);

            MainThread.BeginInvokeOnMainThread(() =>
            {
                TransactionIdLabel.Text = $"{LanguageService.GetString("TransactionID")} {_transactionId}";
                TourNameLabel.Text = tour?.Name ?? $"Tour #{booking.TourId}";
                TourDateLabel.Text = booking.TourDate.ToString("dd/MM/yyyy");
                ParticipantsLabel.Text = $"{booking.NumberOfParticipants}{LanguageService.GetString("PeopleSuffix")}";
                TotalPriceLabel.Text = $"{booking.TotalPrice:N0} đ";
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PaymentSuccessPage] Load error: {ex.Message}");
        }
    }

    private async void OnViewBookingsClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("UserBookingsPage");
    }

    private async void OnGoHomeClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//HomePage");
    }
}

using System.Diagnostics;
using System.Text.Json;
using System.Timers;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

[QueryProperty(nameof(BookingId), "bookingId")]
public partial class PaymentPage : ContentPage
{
    private readonly ApiService _apiService;
    private int _bookingId;
    private Booking? _booking;
    private Tour? _tour;
    private System.Timers.Timer? _timer;
    private int _remainingSeconds = 900; // 15 minutes

    public int BookingId
    {
        get => _bookingId;
        set
        {
            _bookingId = value;
            _ = LoadBookingAsync();
        }
    }

    public PaymentPage()
    {
        InitializeComponent();
        _apiService = new ApiService();
        StartTimer();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _timer?.Stop();
        _timer?.Dispose();
    }

    private async Task LoadBookingAsync()
    {
        try
        {
            // Load booking details
            _booking = await _apiService.GetBookingAsync(_bookingId);
            if (_booking == null)
            {
                await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("BookingNotFound"), LanguageService.GetString("OK"));
                await Shell.Current.GoToAsync("..");
                return;
            }

            // Load tour details
            _tour = await _apiService.GetTourByIdAsync(_booking.TourId);

            // Update UI
            MainThread.BeginInvokeOnMainThread(() =>
            {
                TourNameLabel.Text = _tour?.Name ?? "Tour";
                BookingCodeLabel.Text = _booking.Code ?? $"BK-{_booking.Id}";
                ParticipantsLabel.Text = _booking.NumberOfParticipants.ToString();
                TotalPriceLabel.Text = $"{_booking.TotalPrice:N0} đ";

                // Generate QR Code
                GenerateQrCode();
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentPage] Error loading booking: {ex.Message}");
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("LoadBookingError"), LanguageService.GetString("OK"));
        }
    }

    private void GenerateQrCode()
    {
        try
        {
            var qrData = new
            {
                type = "payment",
                bookingId = _bookingId,
                amount = _booking?.TotalPrice ?? 0,
                tourName = _tour?.Name ?? "Tour",
                expiry = DateTime.UtcNow.AddMinutes(15),
                timestamp = DateTime.UtcNow.Ticks
            };

            var qrJson = System.Text.Json.JsonSerializer.Serialize(qrData);
            var qrImageSource = QrCodeService.GenerateQrCodeImageSource(qrJson);
            
            QrCodeImage.Source = qrImageSource;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentPage] Error generating QR: {ex.Message}");
        }
    }

    private void StartTimer()
    {
        _timer = new System.Timers.Timer(1000); // 1 second
        _timer.Elapsed += OnTimerElapsed;
        _timer.AutoReset = true;
        _timer.Start();
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _remainingSeconds--;
        
        if (_remainingSeconds <= 0)
        {
            _timer?.Stop();
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert(LanguageService.GetString("Notice"), LanguageService.GetString("QRExpired"), LanguageService.GetString("OK"));
                await Shell.Current.GoToAsync("..");
            });
            return;
        }

        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimerLabel.Text = $"{LanguageService.GetString("QRCodeExpiry")} {minutes:D2}:{seconds:D2}";
        });
    }

    private async void OnVnPayClicked(object sender, EventArgs e) => await ProcessPaymentAsync();
    private async void OnMomoClicked(object sender, EventArgs e) => await ProcessPaymentAsync();
    private async void OnPayPalClicked(object sender, EventArgs e) => await ProcessPaymentAsync();

    private async Task ProcessPaymentAsync()
    {
        try
        {
            VnPayButton.IsEnabled = false;
            MomoButton.IsEnabled = false;
            PayPalButton.IsEnabled = false;

            var result = await _apiService.VerifyQrPaymentAsync(_bookingId);
            if (result.Success)
            {
                await Shell.Current.GoToAsync($"PaymentSuccessPage?bookingId={_bookingId}&transactionId={Uri.EscapeDataString(result.TransactionId ?? "")}");
            }
            else
            {
                await DisplayAlert(LanguageService.GetString("PaymentFailed"), result.Message, LanguageService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentPage] error: {ex.Message}");
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PaymentProcessError"), LanguageService.GetString("OK"));
        }
        finally
        {
            VnPayButton.IsEnabled = true;
            MomoButton.IsEnabled = true;
            PayPalButton.IsEnabled = true;
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert(LanguageService.GetString("Notice"),
            LanguageService.GetString("CancelPaymentConfirm"),
            LanguageService.GetString("OK"), LanguageService.GetString("Cancel"));

        if (result)
        {
            try
            {
                await _apiService.CancelBookingAsync(_bookingId, LanguageService.GetString("BookingCancelled"));
                await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("CancelSuccess"), LanguageService.GetString("OK"));
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PaymentPage] Cancel error: {ex.Message}");
                await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("CancelBookingError"), LanguageService.GetString("OK"));
            }
        }
    }
}

using System.Diagnostics;
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
                await DisplayAlert("Lỗi", "Không tìm thấy thông tin booking", "OK");
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
            await DisplayAlert("Lỗi", "Không thể tải thông tin booking", "OK");
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
                await DisplayAlert("Hết hạn", "Mã QR đã hết hạn, vui lòng thử lại", "OK");
                await Shell.Current.GoToAsync("..");
            });
            return;
        }

        var minutes = _remainingSeconds / 60;
        var seconds = _remainingSeconds % 60;
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            TimerLabel.Text = $"Mã QR hết hạn sau: {minutes:D2}:{seconds:D2}";
        });
    }

    private async void OnSimulateSuccessClicked(object sender, EventArgs e)
    {
        try
        {
            // Simulate successful payment
            var result = await _apiService.VerifyQrPaymentAsync(_bookingId);
            
            if (result.Success)
            {
                await DisplayAlert("Thành công", 
                    $"Thanh toán thành công!\nMã giao dịch: {result.TransactionId}", "OK");
                
                // Navigate to Map with tour route
                if (_tour != null)
                {
                    var parameters = new Dictionary<string, object>
                    {
                        { "tourId", _tour.Id.ToString() },
                        { "showRoute", "true" }
                    };
                    await Shell.Current.GoToAsync($"///mappage?tourId={_tour.Id}&showRoute=true");
                }
                else
                {
                    await Shell.Current.GoToAsync("///mappage");
                }
            }
            else
            {
                await DisplayAlert("Thất bại", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PaymentPage] Payment error: {ex.Message}");
            await DisplayAlert("Lỗi", "Có lỗi xảy ra khi xử lý thanh toán", "OK");
        }
    }

    private async void OnSimulateFailClicked(object sender, EventArgs e)
    {
        await DisplayAlert("Thất bại", 
            "Giả lập: Quét mã không thành công. Vui lòng thử lại.", "OK");
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        var result = await DisplayAlert("Xác nhận", 
            "Bạn có chắc muốn hủy thanh toán?\nBooking của bạn sẽ bị hủy.", 
            "Có", "Không");
        
        if (result)
        {
            try
            {
                await _apiService.CancelBookingAsync(_bookingId, "Người dùng hủy thanh toán");
                await DisplayAlert("Đã hủy", "Booking đã được hủy thành công", "OK");
                await Shell.Current.GoToAsync("..");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PaymentPage] Cancel error: {ex.Message}");
                await DisplayAlert("Lỗi", "Không thể hủy booking", "OK");
            }
        }
    }
}

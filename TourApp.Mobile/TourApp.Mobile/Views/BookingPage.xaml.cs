using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

[QueryProperty(nameof(TourId), "tourId")]
public partial class BookingPage : ContentPage
{
    private readonly ApiService _apiService;
    private Tour? _currentTour;
    private int _tourId;
    private int _participants = 1;
    private decimal _tourPrice = 0;

    public DateTime MinimumDate { get; set; } = DateTime.Now.Date;

    public int TourId
    {
        get => _tourId;
        set
        {
            _tourId = value;
            _ = LoadTourData();
        }
    }

    public BookingPage()
    {
        InitializeComponent();
        _apiService = new ApiService();
        BindingContext = this;
    }

    private async Task LoadTourData()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            _currentTour = await _apiService.GetTourByIdAsync(TourId);

            if (_currentTour != null)
            {
                _tourPrice = _currentTour.Price;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TourNameLabel.Text = _currentTour.Name;
                    PriceLabel.Text = $"{_tourPrice:N0} đ";
                    UpdateTotalPrice();
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[BookingPage] error: {ex.Message}");
        }
    }

    private void OnDecreaseParticipantsClicked(object sender, EventArgs e)
    {
        if (_participants > 1)
        {
            _participants--;
            ParticipantsLabel.Text = _participants.ToString();
            UpdateTotalPrice();
        }
    }

    private void OnIncreaseParticipantsClicked(object sender, EventArgs e)
    {
        if (_currentTour != null && _participants < _currentTour.MaxParticipants)
        {
            _participants++;
            ParticipantsLabel.Text = _participants.ToString();
            UpdateTotalPrice();
        }
        else if (_currentTour != null)
        {
            DisplayAlert("Thông báo", $"Tour này tối đa {_currentTour.MaxParticipants} người.", "OK");
        }
    }

    private void UpdateTotalPrice()
    {
        var total = _tourPrice * _participants;
        TotalPriceLabel.Text = $"{total:N0} đ";
    }

    private async void OnConfirmBookingClicked(object sender, EventArgs e)
    {
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            await DisplayAlert("Lỗi", "Vui lòng đăng nhập để đặt tour", "OK");
            return;
        }

        if (_currentTour == null) return;

        ConfirmBookingButton.IsEnabled = false;
        ConfirmBookingButton.Text = "Đang xử lý...";

        var booking = new Booking
        {
            TourId = _currentTour.Id,
            UserId = AuthService.CurrentUser.Id,
            NumberOfParticipants = _participants,
            TourDate = TourDatePicker.Date,
            BookingDate = DateTime.Now,
            TotalPrice = _tourPrice * _participants,
            Status = "Pending",
            Notes = NotesEditor.Text
        };

        var result = await _apiService.BookTourAsync(booking);

        if (result.Success)
        {
            await DisplayAlert("Thành công", "Cảm ơn bạn đã đặt tour. Vui lòng đợi quản trị viên xác nhận!", "OK");
            await Shell.Current.Navigation.PopToRootAsync();
        }
        else
        {
            await DisplayAlert("Lỗi", result.Message, "OK");
            ConfirmBookingButton.IsEnabled = true;
            ConfirmBookingButton.Text = "Xác nhận Đặt Tour";
        }
    }
}

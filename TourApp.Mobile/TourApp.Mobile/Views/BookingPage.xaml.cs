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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        RestorePendingBookingIfExists();
    }

    private void RestorePendingBookingIfExists()
    {
        // Khôi phục state từ pending booking sau khi login
        if (PendingBookingService.HasPendingBooking() && 
            PendingBookingService.PendingBooking?.TourId == _tourId)
        {
            var pending = PendingBookingService.PendingBooking!;
            
            _participants = pending.Participants;
            
            MainThread.BeginInvokeOnMainThread(() =>
            {
                ParticipantsLabel.Text = _participants.ToString();
                TourDatePicker.Date = pending.TourDate;
                if (!string.IsNullOrEmpty(pending.Notes))
                    NotesEditor.Text = pending.Notes;
                UpdateTotalPrice();
            });
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
            DisplayAlert(LanguageService.GetString("Notice"), LanguageService.GetString("MaxParticipants", _currentTour.MaxParticipants), LanguageService.GetString("OK"));
        }
    }

    private void UpdateTotalPrice()
    {
        var total = _tourPrice * _participants;
        TotalPriceLabel.Text = $"{total:N0} đ";
    }

    private async void OnConfirmBookingClicked(object sender, EventArgs e)
    {
        // Check nếu đang ở chế độ khách (chưa đăng nhập)
        if (!AuthService.IsLoggedIn || AuthService.CurrentUser == null)
        {
            // Lưu thông tin booking đang dang dở để sau khi login sẽ tiếp tục
            PendingBookingService.Save(
                _tourId,
                _participants,
                TourDatePicker.Date,
                NotesEditor.Text,
                _tourPrice * _participants
            );

            var result = await DisplayAlert(
                LanguageService.GetString("LoginRequired"),
                LanguageService.GetString("LoginToBook"),
                LanguageService.GetString("Login"),
                LanguageService.GetString("Cancel")
            );

            if (result)
            {
                // Chuyển về trang login
                await Shell.Current.Navigation.PushAsync(new Auth.LoginPage());
            }
            return;
        }

        await SubmitBookingAsync();
    }

    private async Task SubmitBookingAsync()
    {
        if (_currentTour == null) return;

        ConfirmBookingButton.IsEnabled = false;
        ConfirmBookingButton.Text = LanguageService.GetString("Processing");

        var booking = new Booking
        {
            TourId = _currentTour.Id,
            UserId = AuthService.CurrentUser!.Id,
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
            // Xóa pending booking nếu có
            PendingBookingService.Clear();
            await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("BookingSuccess"), LanguageService.GetString("OK"));
            await Shell.Current.Navigation.PopToRootAsync();
        }
        else
        {
            await DisplayAlert(LanguageService.GetString("Error"), result.Message, LanguageService.GetString("OK"));
            ConfirmBookingButton.IsEnabled = true;
            ConfirmBookingButton.Text = LanguageService.GetString("ConfirmBooking");
        }
    }
}

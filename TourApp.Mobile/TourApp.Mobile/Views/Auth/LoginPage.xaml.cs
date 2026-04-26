using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views.Auth;

public partial class LoginPage : ContentPage
{
    private bool _isPasswordVisible = false;

    public LoginPage()
    {
        InitializeComponent();
    }

    private void OnShowPasswordTapped(object sender, EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordEntry.IsPassword = !_isPasswordVisible;
        ShowPasswordLabel.Text = _isPasswordVisible ? "🙈" : "👁";
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("LoginRequiredUsername"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("LoginRequiredPassword"), LanguageService.GetString("OK"));
            return;
        }

        // Show loading
        LoginButton.IsEnabled = false;
        LoginButton.Text = LanguageService.GetString("Loading");

        try
        {
            var authService = new AuthService();
            var result = await authService.LoginAsync(username, password);

            if (result.Success && result.User != null)
            {
                // Kiểm tra nếu có booking đang dang dở
                if (PendingBookingService.HasPendingBooking())
                {
                    var pending = PendingBookingService.PendingBooking!;
                    MainThread.BeginInvokeOnMainThread(() => {
                        // Navigate về BookingPage với TourId đã lưu
                        Shell.Current.GoToAsync($"booking?tourId={pending.TourId}");
                    });
                }
                else
                {
                    // Navigate to main app bình thường
                    MainThread.BeginInvokeOnMainThread(() => {
                        AppNavigation.SetRootPage(new AppShell());
                    });
                }
            }
            else
            {
                // Phân biệt rõ lỗi mạng vs lỗi sai tài khoản
                var title = result.IsNetworkError 
                    ? LanguageService.GetString("ServerError") 
                    : LanguageService.GetString("LoginFailed");
                await DisplayAlert(title, result.Message, LanguageService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(LanguageService.GetString("ServerError"), LanguageService.GetString("ConnectionErrorDetail", ex.Message), LanguageService.GetString("OK"));
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = LanguageService.GetString("LoginButton");
        }
    }

    private async void OnSignUpTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignUpPage());
    }

    private async void OnGuestLoginClicked(object sender, EventArgs e)
    {
        // Tạo user khách
        var guestUser = AuthService.CreateGuestUser();

        // Lưu thông tin user khách
        AuthService.SetCurrentUser(guestUser);

        // Khởi động session cho khách
        var guestId = $"guest_{Guid.NewGuid().ToString("N")[..8]}";
        UserSessionService.StartSession(null, LanguageService.GetString("Guest"), guestId);

        // Chuyển đến app chính
        AppNavigation.SetRootPage(new AppShell());
    }
}

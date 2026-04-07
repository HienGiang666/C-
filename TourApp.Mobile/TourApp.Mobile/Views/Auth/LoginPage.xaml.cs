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
            await DisplayAlert("Lỗi", "Vui lòng nhập tên đăng nhập", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập mật khẩu", "OK");
            return;
        }

        // Show loading
        LoginButton.IsEnabled = false;
        LoginButton.Text = "Đang đăng nhập...";

        try
        {
            var authService = new AuthService();
            var result = await authService.LoginAsync(username, password);

            if (result.Success && result.User != null)
            {
                // Navigate to main app
                Application.Current!.MainPage = new AppShell();
            }
            else
            {
                await DisplayAlert("Đăng nhập thất bại", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Có lỗi xảy ra: {ex.Message}", "OK");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = "LOG IN";
        }
    }

    private async void OnForgotPasswordTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ForgotPasswordPage());
    }

    private async void OnSignUpTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new SignUpPage());
    }
}

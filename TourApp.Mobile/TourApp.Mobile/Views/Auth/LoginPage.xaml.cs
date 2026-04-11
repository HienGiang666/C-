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
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("UsernamePlaceholder"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordPlaceholder"), LanguageService.GetString("OK"));
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
                // Navigate to main app
                AppNavigation.SetRootPage(new AppShell());
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
            await DisplayAlert(LanguageService.GetString("ServerError"), $"Lỗi kết nối: {ex.Message}", LanguageService.GetString("OK"));
        }
        finally
        {
            LoginButton.IsEnabled = true;
            LoginButton.Text = LanguageService.GetString("LoginButton").ToUpper();
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

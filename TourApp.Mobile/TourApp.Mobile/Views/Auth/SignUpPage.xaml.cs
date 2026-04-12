using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views.Auth;

public partial class SignUpPage : ContentPage
{
    private bool _isPasswordVisible = false;
    private bool _isConfirmPasswordVisible = false;

    public SignUpPage()
    {
        InitializeComponent();
    }

    private void OnShowPasswordTapped(object sender, EventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        PasswordEntry.IsPassword = !_isPasswordVisible;
        ShowPasswordLabel.Text = _isPasswordVisible ? "🙈" : "👁";
    }

    private void OnShowConfirmPasswordTapped(object sender, EventArgs e)
    {
        _isConfirmPasswordVisible = !_isConfirmPasswordVisible;
        ConfirmPasswordEntry.IsPassword = !_isConfirmPasswordVisible;
        ShowConfirmPasswordLabel.Text = _isConfirmPasswordVisible ? "🙈" : "👁";
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        var fullName = FullNameEntry.Text?.Trim();
        var username = UsernameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;
        var confirmPassword = ConfirmPasswordEntry.Text;

        // Validation
        if (string.IsNullOrWhiteSpace(fullName))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("FullNameRequired"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("UsernamePlaceholder"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("EmailRequired"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordPlaceholder"), LanguageService.GetString("OK"));
            return;
        }

        if (password.Length < 6)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordTooShort"), LanguageService.GetString("OK"));
            return;
        }

        if (password != confirmPassword)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordMismatch"), LanguageService.GetString("OK"));
            return;
        }

        // Show loading
        SignUpButton.IsEnabled = false;
        SignUpButton.Text = LanguageService.GetString("Loading");

        try
        {
            var authService = new AuthService();
            var result = await authService.RegisterAsync(fullName, username, email, password);

            if (result.Success && result.User != null)
            {
                await DisplayAlert("Thành công", "Đăng ký tài khoản thành công!", "OK");
                // Navigate to main app
                MainThread.BeginInvokeOnMainThread(() => {
                    AppNavigation.SetRootPage(new AppShell());
                });
            }
            else
            {
                await DisplayAlert("Đăng ký thất bại", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(LanguageService.GetString("Error"), $"{LanguageService.GetString("Error")}: {ex.Message}", LanguageService.GetString("OK"));
        }
        finally
        {
            SignUpButton.IsEnabled = true;
            SignUpButton.Text = LanguageService.GetString("RegisterButton").ToUpper();
        }
    }
}

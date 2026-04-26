using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views.Auth;

public partial class ResetPasswordPage : ContentPage
{
    private readonly string _email;
    private readonly string _expectedCode;

    public ResetPasswordPage(string email, string expectedCode)
    {
        InitializeComponent();
        _email = email;
        _expectedCode = expectedCode;
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnResetClicked(object sender, EventArgs e)
    {
        var code = CodeEntry.Text?.Trim();
        var newPassword = NewPasswordEntry.Text;
        var confirmPassword = ConfirmPasswordEntry.Text;

        // Validation
        if (string.IsNullOrWhiteSpace(code))
        {
            await DisplayAlert(
                LanguageService.GetString("Error"), 
                LanguageService.GetString("ResetCodeRequired"), 
                LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            await DisplayAlert(
                LanguageService.GetString("Error"), 
                LanguageService.GetString("NewPasswordRequired"), 
                LanguageService.GetString("OK"));
            return;
        }

        if (newPassword.Length < 6)
        {
            await DisplayAlert(
                LanguageService.GetString("Error"), 
                LanguageService.GetString("PasswordTooShort"), 
                LanguageService.GetString("OK"));
            return;
        }

        if (newPassword != confirmPassword)
        {
            await DisplayAlert(
                LanguageService.GetString("Error"), 
                LanguageService.GetString("PasswordMismatch"), 
                LanguageService.GetString("OK"));
            return;
        }

        // Show loading
        ResetButton.IsEnabled = false;
        ResetButton.Text = LanguageService.GetString("Loading");

        try
        {
            var authService = new AuthService();
            var result = await authService.ResetPasswordAsync(_email, code, newPassword);

            if (result.Success)
            {
                await DisplayAlert(
                    LanguageService.GetString("Success"), 
                    result.Message, 
                    LanguageService.GetString("OK"));
                
                // Navigate back to login
                await Navigation.PopToRootAsync();
            }
            else
            {
                var title = result.IsNetworkError 
                    ? LanguageService.GetString("ServerError") 
                    : LanguageService.GetString("Error");
                await DisplayAlert(title, result.Message, LanguageService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert(
                LanguageService.GetString("ServerError"), 
                LanguageService.GetString("ConnectionErrorDetail", ex.Message), 
                LanguageService.GetString("OK"));
        }
        finally
        {
            ResetButton.IsEnabled = true;
            ResetButton.Text = LanguageService.GetString("ResetPasswordButton");
        }
    }
}

using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views.Auth;

public partial class ForgotPasswordPage : ContentPage
{
    public ForgotPasswordPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSendCodeClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            await DisplayAlert(
                LanguageService.GetString("Error"), 
                LanguageService.GetString("EmailRequired"), 
                LanguageService.GetString("OK"));
            return;
        }

        // Show loading
        var sendButton = (Microsoft.Maui.Controls.Button)sender;
        sendButton.IsEnabled = false;
        sendButton.Text = LanguageService.GetString("Loading");

        try
        {
            var authService = new AuthService();
            var result = await authService.ForgotPasswordAsync(email);

            if (result.Success)
            {
                // Demo: Show the reset code in alert (instead of sending email)
                var message = result.DemoCode != null 
                    ? $"{LanguageService.GetString("ResetCodeDemo")}\n\n{LanguageService.GetString("DemoCode")}: {result.DemoCode}"
                    : result.Message;

                await DisplayAlert(
                    LanguageService.GetString("Success"), 
                    message, 
                    LanguageService.GetString("OK"));

                // Navigate to reset password page with email and code
                if (result.DemoCode != null)
                {
                    await Navigation.PushAsync(new ResetPasswordPage(email, result.DemoCode));
                }
                else
                {
                    // If no demo code, we might need a different flow or just wait for email
                    // For now, let's assume we need a code to proceed
                    await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("NoResetCode"), LanguageService.GetString("OK"));
                }
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
            sendButton.IsEnabled = true;
            sendButton.Text = LanguageService.GetString("SendCode");
        }
    }
}

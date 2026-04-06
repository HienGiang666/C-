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
        await Shell.Current.GoToAsync("VerificationPage");
    }
}

namespace TourApp.Mobile.Views.Auth;

public partial class SignUpPage : ContentPage
{
    public SignUpPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSignUpClicked(object sender, EventArgs e)
    {
        // Go straight to Home after successful signup check
        await Shell.Current.GoToAsync("///HomePage");
    }
}

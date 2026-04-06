namespace TourApp.Mobile.Views.Auth;

public partial class VerificationPage : ContentPage
{
    public VerificationPage()
    {
        InitializeComponent();
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnVerifyClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///HomePage"); // Login complete
    }
}

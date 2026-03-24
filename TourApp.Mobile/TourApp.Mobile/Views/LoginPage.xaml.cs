namespace TourApp.Mobile.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage()
    {
        InitializeComponent();
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        var email = EmailEntry.Text;
        var pass = PasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            await this.DisplayAlert("Lỗi", "Vui lòng nhập Email và Mật khẩu!", "OK");
            return;
        }

        // Mock Login - dismiss modal to go back to Home
        await Navigation.PopModalAsync();
    }

    private async void OnRegisterTapped(object? sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new RegisterPage());
    }
}


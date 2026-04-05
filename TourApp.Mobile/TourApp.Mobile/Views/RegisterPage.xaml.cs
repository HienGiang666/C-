namespace TourApp.Mobile.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        var name = NameEntry.Text;
        var email = EmailEntry.Text;
        var pass = PasswordEntry.Text;
        var confirm = ConfirmPasswordEntry.Text;

        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(pass))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập đủ thông tin!", "OK");
            return;
        }

        if (pass != confirm)
        {
            await DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp!", "OK");
            return;
        }

        // Mock Register
        await DisplayAlert("Thành công", "Đăng ký thành công! Đang đăng nhập...", "OK");
        // Pop Register, then pop Login to land on Home
        await Navigation.PopAsync(); // back to Login
        await Application.Current!.Windows[0].Navigation.PopModalAsync(); // dismiss Login modal
    }
}

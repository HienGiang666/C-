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
            await DisplayAlert("Lỗi", "Vui lòng nhập họ tên", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập tên đăng nhập", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập email", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập mật khẩu", "OK");
            return;
        }

        if (password.Length < 6)
        {
            await DisplayAlert("Lỗi", "Mật khẩu phải có ít nhất 6 ký tự", "OK");
            return;
        }

        if (password != confirmPassword)
        {
            await DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp", "OK");
            return;
        }

        // Show loading
        SignUpButton.IsEnabled = false;
        SignUpButton.Text = "Đang đăng ký...";

        try
        {
            var authService = new AuthService();
            var result = await authService.RegisterAsync(fullName, username, email, password);

            if (result.Success && result.User != null)
            {
                await DisplayAlert("Thành công", "Đăng ký tài khoản thành công!", "OK");
                // Navigate to main app
                Application.Current!.MainPage = new AppShell();
            }
            else
            {
                await DisplayAlert("Đăng ký thất bại", result.Message, "OK");
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Lỗi", $"Có lỗi xảy ra: {ex.Message}", "OK");
        }
        finally
        {
            SignUpButton.IsEnabled = true;
            SignUpButton.Text = "SIGN UP";
        }
    }
}

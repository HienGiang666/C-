using System.Net.Http.Json;
using System.Text.Json;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public ChangePasswordPage()
    {
        InitializeComponent();
    }

    private async void OnChangePasswordClicked(object sender, EventArgs e)
    {
        var user = AuthService.CurrentUser;
        if (user == null)
        {
            await DisplayAlert("Lỗi", "Không tìm thấy thông tin người dùng", "OK");
            return;
        }

        var currentPassword = CurrentPasswordEntry.Text?.Trim();
        var newPassword = NewPasswordEntry.Text?.Trim();
        var confirmPassword = ConfirmPasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            await DisplayAlert("Lỗi", "Vui lòng nhập mật khẩu hiện tại", "OK");
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            await DisplayAlert("Lỗi", "Mật khẩu mới phải có ít nhất 6 ký tự", "OK");
            return;
        }

        if (newPassword != confirmPassword)
        {
            await DisplayAlert("Lỗi", "Mật khẩu xác nhận không khớp", "OK");
            return;
        }

        SaveButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var baseUrl = ApiService.BaseUrl;
            using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

            // Step 1: Verify current password by attempting login
            var loginRequest = new { Username = user.Username, Password = currentPassword };
            var loginResponse = await httpClient.PostAsJsonAsync("/api/user/login", loginRequest);

            if (!loginResponse.IsSuccessStatusCode)
            {
                await DisplayAlert("Lỗi", "Mật khẩu hiện tại không đúng", "OK");
                return;
            }

            // Step 2: Update password via PUT /api/user/{id}
            // API hashes the password if it's different from existing hash
            var updateData = new
            {
                Id = user.Id,
                FullName = user.FullName,
                Username = user.Username,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                DateOfBirth = user.DateOfBirth,
                Role = user.Role ?? "Customer",
                IsActive = true,
                PasswordHash = newPassword // API sẽ hash nếu khác hash hiện tại
            };

            var response = await httpClient.PutAsJsonAsync($"/api/user/{user.Id}", updateData);

            if (response.IsSuccessStatusCode)
            {
                await DisplayAlert("Thành công", "Đổi mật khẩu thành công!", "OK");
                await Navigation.PopAsync();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await DisplayAlert("Lỗi", $"Đổi mật khẩu thất bại: {errorContent}", "OK");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChangePasswordPage] Error: {ex.Message}");
            await DisplayAlert("Lỗi", $"Lỗi kết nối: {ex.Message}", "OK");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }
}

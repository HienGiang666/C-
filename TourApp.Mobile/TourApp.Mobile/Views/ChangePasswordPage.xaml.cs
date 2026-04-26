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
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("UserNotFound"), LanguageService.GetString("OK"));
            return;
        }

        var currentPassword = CurrentPasswordEntry.Text?.Trim();
        var newPassword = NewPasswordEntry.Text?.Trim();
        var confirmPassword = ConfirmPasswordEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(currentPassword))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("CurrentPasswordRequired"), LanguageService.GetString("OK"));
            return;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordTooShort"), LanguageService.GetString("OK"));
            return;
        }

        if (newPassword != confirmPassword)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("PasswordMismatch"), LanguageService.GetString("OK"));
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
                await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("CurrentPasswordWrong"), LanguageService.GetString("OK"));
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
                await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("ChangePasswordSuccess"), LanguageService.GetString("OK"));
                await Navigation.PopAsync();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("ChangePasswordFailed", errorContent), LanguageService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ChangePasswordPage] Error: {ex.Message}");
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("ConnectionErrorDetail", ex.Message), LanguageService.GetString("OK"));
        }
        finally
        {
            SaveButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }
}

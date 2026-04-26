using System.Net.Http.Json;
using System.Text.Json;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class EditProfilePage : ContentPage
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    public DateTime Today => DateTime.Today;

    public EditProfilePage()
    {
        InitializeComponent();
        BindingContext = this;
        LoadCurrentUserData();
    }

    private void LoadCurrentUserData()
    {
        var user = AuthService.CurrentUser;
        if (user == null) return;

        FullNameEntry.Text = user.FullName ?? "";
        EmailEntry.Text = user.Email ?? "";
        PhoneEntry.Text = user.PhoneNumber ?? "";
        AddressEntry.Text = user.Address ?? "";

        if (user.DateOfBirth.HasValue)
            DateOfBirthPicker.Date = user.DateOfBirth.Value;
        else
            DateOfBirthPicker.Date = DateTime.Today.AddYears(-20);
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        var user = AuthService.CurrentUser;
        if (user == null)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("UserNotFound"), LanguageService.GetString("OK"));
            return;
        }

        var fullName = FullNameEntry.Text?.Trim();
        var email = EmailEntry.Text?.Trim();
        var phone = PhoneEntry.Text?.Trim();
        var address = AddressEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(fullName))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("FullNameRequired"), LanguageService.GetString("OK"));
            return;
        }

        SaveButton.IsEnabled = false;
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;

        try
        {
            var baseUrl = ApiService.BaseUrl;
            using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(10) };

            var updateData = new
            {
                Id = user.Id,
                FullName = fullName,
                Username = user.Username,
                Email = email,
                PhoneNumber = phone,
                Address = address,
                DateOfBirth = DateOfBirthPicker.Date,
                Role = user.Role ?? "Customer",
                IsActive = true,
                PasswordHash = "" // Empty = keep current password
            };

            var response = await httpClient.PutAsJsonAsync($"/api/user/{user.Id}", updateData);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var updatedUser = JsonSerializer.Deserialize<User>(content, JsonOpts);

                if (updatedUser != null)
                {
                    // Update local session
                    AuthService.CurrentUser.FullName = updatedUser.FullName;
                    AuthService.CurrentUser.Email = updatedUser.Email;
                    AuthService.CurrentUser.PhoneNumber = updatedUser.PhoneNumber;
                    AuthService.CurrentUser.Address = updatedUser.Address;
                    AuthService.CurrentUser.DateOfBirth = updatedUser.DateOfBirth;

                    // Update Preferences
                    Preferences.Default.Set("user_fullname", updatedUser.FullName ?? "");
                    Preferences.Default.Set("user_email", updatedUser.Email ?? "");
                }

                await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("UpdateSuccess"), LanguageService.GetString("OK"));
                await Navigation.PopAsync();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("UpdateFailed", errorContent), LanguageService.GetString("OK"));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[EditProfilePage] Save error: {ex.Message}");
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("ConnectionError", ex.Message), LanguageService.GetString("OK"));
        }
        finally
        {
            SaveButton.IsEnabled = true;
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }
}

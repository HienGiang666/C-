using Microsoft.Maui;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private List<Language>? _languages;
    private readonly ApiService _apiService;

    public ProfilePage()
    {
        InitializeComponent();
        _apiService = new ApiService();
        
        // Load languages
        _ = LoadLanguagesAsync();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Update user info
        if (AuthService.CurrentUser != null)
        {
            UserNameLabel.Text = AuthService.CurrentUser.DisplayName;
            UserEmailLabel.Text = AuthService.CurrentUser.Email ?? "Chưa cập nhật email";
        }
        
        // Update language display
        var currentLang = Preferences.Default.Get("app_lang", "vi");
        var langName = _languages?.FirstOrDefault(l => l.Code == currentLang)?.NativeName ?? "Tiếng Việt";
        LanguageLabel.Text = langName;
    }
    
    private async Task LoadLanguagesAsync()
    {
        try
        {
            _languages = await _apiService.GetLanguagesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] LoadLanguages error: {ex.Message}");
            // Fallback languages
            _languages = new List<Language>
            {
                new Language { Id = 1, Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt" },
                new Language { Id = 2, Code = "en", Name = "English", NativeName = "English" },
                new Language { Id = 3, Code = "zh", Name = "Chinese", NativeName = "中文" },
                new Language { Id = 4, Code = "ja", Name = "Japanese", NativeName = "日本語" },
                new Language { Id = 5, Code = "ko", Name = "Korean", NativeName = "한국어" },
                new Language { Id = 6, Code = "fr", Name = "French", NativeName = "Français" },
                new Language { Id = 7, Code = "de", Name = "German", NativeName = "Deutsch" },
                new Language { Id = 8, Code = "es", Name = "Spanish", NativeName = "Español" },
                new Language { Id = 9, Code = "th", Name = "Thai", NativeName = "ไทย" },
                new Language { Id = 10, Code = "ru", Name = "Russian", NativeName = "Русский" }
            };
        }
    }

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        if (_languages == null || !_languages.Any())
        {
            await DisplayAlert("Lỗi", "Không thể tải danh sách ngôn ngữ", "OK");
            return;
        }
        
        var activeLangs = _languages.Where(l => l.IsActive).ToList();
        var options = activeLangs.Select(l => $"{l.NativeName} ({l.Code})").ToArray();
        
        var action = await DisplayActionSheet("Chọn Ngôn Ngữ", "Huỷ", null, options);
        if (!string.IsNullOrEmpty(action) && action != "Huỷ")
        {
            var selectedCode = action.Split('(')[1].TrimEnd(')');
            var selectedLang = activeLangs.FirstOrDefault(l => l.Code == selectedCode);
            
            if (selectedLang != null)
            {
                Preferences.Default.Set("app_lang", selectedCode);
                LanguageLabel.Text = selectedLang.NativeName;
                
                // Update geofence service language
                var geofence = IPlatformApplication.Current?.Services.GetService<GeofenceService>();
                if (geofence != null) geofence.CurrentLanguage = selectedCode;
                
                await DisplayAlert("Ngôn ngữ", $"Đã thay đổi sang: {selectedLang.NativeName}", "OK");
            }
        }
    }

    private async void OnHistoryTapped(object sender, EventArgs e)
    {
        // Navigate to tour history page
        await DisplayAlert("Lịch sử", "Tính năng đang được phát triển", "OK");
    }

    private async void OnFavoritesTapped(object sender, EventArgs e)
    {
        // Navigate to favorites page
        await DisplayAlert("Yêu thích", "Tính năng đang được phát triển", "OK");
    }

    private async void OnChangeIpTapped(object sender, EventArgs e)
    {
        var currentIp = ApiService.BaseUrl;
        var result = await DisplayPromptAsync("Chỉnh IP", "Nhập địa chỉ máy chủ (VD: http://192.168.1.5:5254):", 
            "Lưu", "Huỷ", currentIp);
        
        if (!string.IsNullOrWhiteSpace(result))
        {
            ApiService.BaseUrl = result.Trim();
            await DisplayAlert("Thành công", "Đã cập nhật địa chỉ máy chủ", "OK");
        }
    }

    private async void OnSettingsTapped(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Cài đặt", "Bạn muốn xoá dữ liệu tạm lưu (Cache) của ứng dụng?", "Xoá", "Huỷ");
        if (confirm)
        {
            Preferences.Default.Clear();
            await DisplayAlert("Thành công", "Đã xoá dữ liệu tạm.", "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Đăng xuất", "Bạn có chắc chắn muốn đăng xuất?", "Đăng xuất", "Huỷ");
        if (confirm)
        {
            AuthService.Logout();
            Application.Current!.MainPage = new NavigationPage(new Views.Auth.LoginPage());
        }
    }
}

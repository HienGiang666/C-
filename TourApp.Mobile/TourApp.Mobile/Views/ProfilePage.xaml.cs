using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    private List<LanguageService.LanguageInfo>? _languages;
    private readonly ApiService _apiService;

    public ProfilePage()
    {
        InitializeComponent();
        _apiService = new ApiService();
        
        // Load languages từ API hoặc fallback
        _ = LoadLanguagesAsync();
        
        // Subscribe to language changes để update UI
        LanguageService.LanguageChanged += OnLanguageChanged;

        // Offline banner
        NetworkService.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner();
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(UpdateOfflineBanner);
    }

    private void UpdateOfflineBanner()
    {
        OfflineBanner.IsVisible = !NetworkService.IsConnected;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        NetworkService.ConnectivityChanged -= OnConnectivityChanged;
    }
    
    private void OnLanguageChanged(object? sender, string newLang)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (LanguageLabel == null) return;
                
                // Update language label display
                var langName = _languages?.FirstOrDefault(l => l.Code == newLang)?.NativeName 
                               ?? LanguageService.GetLanguageName(newLang);
                LanguageLabel.Text = langName;

                // Update guest mode labels when language changes
                if (AuthService.IsGuestMode)
                {
                    UserNameLabel.Text = LanguageService.GetString("Guest");
                    UserEmailLabel.Text = LanguageService.GetString("GuestMode");
                }

                // Notify user about language change
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] UI updated to language: {newLang}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ProfilePage] OnLanguageChanged error: {ex.Message}");
            }
        });
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        var isGuest = AuthService.IsGuestMode;
        
        // Update user info
        if (AuthService.CurrentUser != null)
        {
            UserNameLabel.Text = isGuest 
                ? LanguageService.GetString("Guest") 
                : AuthService.CurrentUser.DisplayName;
            UserEmailLabel.Text = isGuest 
                ? LanguageService.GetString("GuestMode") 
                : (AuthService.CurrentUser.Email ?? LanguageService.GetString("NoEmail"));
        }
        
        // Ẩn/hiện các mục theo guest mode
        EditProfileButton.IsVisible = !isGuest;
        HistoryRow.IsVisible = true; // Guest cũng có thể xem lịch sử đặt tour
        HistoryDivider.IsVisible = true;
        ChangePasswordRow.IsVisible = !isGuest;
        LogoutButton.IsVisible = !isGuest;
        GuestButtonsLayout.IsVisible = isGuest;
        
        // Update language display theo ngôn ngữ hiện tại
        var currentLang = LanguageService.CurrentLanguage;
        var langName = _languages?.FirstOrDefault(l => l.Code == currentLang)?.NativeName 
                       ?? LanguageService.GetLanguageName(currentLang);
        LanguageLabel.Text = langName;
        
        // Update all localized text on this page
        UpdateLocalizedText();
    }
    
    private void UpdateLocalizedText()
    {
        // Cập nhật các text đã localize
        // Chỉ cập nhật các label có thể thay đổi, không cập nhật static XAML
    }
    
    private async Task LoadLanguagesAsync()
    {
        try
        {
            var apiLanguages = await _apiService.GetLanguagesAsync();
            if (apiLanguages?.Any() == true)
            {
                // Chuyển đổi từ API model sang LanguageInfo
                _languages = apiLanguages.Select(l => new LanguageService.LanguageInfo 
                { 
                    Code = l.Code ?? "vi", 
                    Name = l.Name ?? "Vietnamese", 
                    NativeName = l.NativeName ?? l.Name ?? "Tiếng Việt",
                    IsActive = l.IsActive 
                }).ToList();
            }
            else
            {
                // Fallback: dùng danh sách từ LanguageService
                _languages = LanguageService.GetActiveLanguages();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ProfilePage] LoadLanguages error: {ex.Message}");
            // Fallback languages từ LanguageService
            _languages = LanguageService.GetActiveLanguages();
        }
    }

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        if (_languages == null || !_languages.Any())
        {
            // Fallback nếu chưa load được
            _languages = LanguageService.GetActiveLanguages();
        }
        
        var activeLangs = _languages.Where(l => l.IsActive).ToList();
        var options = activeLangs.Select(l => $"{l.NativeName} ({l.Code})").ToArray();
        
        var action = await DisplayActionSheet(LanguageService.GetString("SelectLanguage"), LanguageService.GetString("Cancel"), null, options);
        if (!string.IsNullOrEmpty(action) && action != LanguageService.GetString("Cancel"))
        {
            var selectedCode = action.Split('(')[1].TrimEnd(')');
            var selectedLang = activeLangs.FirstOrDefault(l => l.Code == selectedCode);
            
            if (selectedLang != null)
            {
                // Set language - tự động sync với GeofenceService và broadcast event
                LanguageService.CurrentLanguage = selectedCode;
                
                // Update UI ngay lập tức
                LanguageLabel.Text = selectedLang.NativeName;
                
                // Thông báo cho user biết ngôn ngữ đã đổi và TTS cũng sẽ đổi theo
                await DisplayAlert(LanguageService.GetString("LanguageChanged"), 
                    LanguageService.GetString("LanguageChangedMessage", selectedLang.NativeName, selectedLang.NativeName), 
                    LanguageService.GetString("OK"));
            }
        }
    }

    private async void OnHistoryTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new UserBookingsPage());
    }

    private async void OnEditProfileTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new EditProfilePage());
    }

    private async void OnChangePasswordTapped(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new ChangePasswordPage());
    }

    private async void OnFavoritesTapped(object sender, EventArgs e)
    {
        var action = await DisplayActionSheet(LanguageService.GetString("Favorites"), LanguageService.GetString("Close"), null, 
            LanguageService.GetString("FavoritePOIs"), 
            LanguageService.GetString("FavoriteTours"));
        
        if (action == LanguageService.GetString("FavoritePOIs") || action == LanguageService.GetString("FavoriteTours"))
        {
            await DisplayAlert(LanguageService.GetString("Favorites"), 
                LanguageService.GetString("FavoritesDesc"), 
                LanguageService.GetString("OK"));
        }
    }

    private async void OnChangeIpTapped(object sender, EventArgs e)
    {
        var currentIp = ApiService.BaseUrl;
        var result = await DisplayPromptAsync(LanguageService.GetString("ChangeServerIP"), 
            LanguageService.GetString("ServerIPHint"), 
            LanguageService.GetString("Save"), LanguageService.GetString("Cancel"), currentIp);
        
        if (!string.IsNullOrWhiteSpace(result))
        {
            ApiService.BaseUrl = result.Trim();
            await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("ServerIPUpdated"), LanguageService.GetString("OK"));
        }
    }

    private async void OnSettingsTapped(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(LanguageService.GetString("Settings"), 
            LanguageService.GetString("ClearCacheConfirm"), 
            LanguageService.GetString("OK"), LanguageService.GetString("Cancel"));
        if (confirm)
        {
            Preferences.Default.Clear();
            await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("CacheCleared"), LanguageService.GetString("OK"));
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert(LanguageService.GetString("Logout"), 
            LanguageService.GetString("LogoutConfirm"), 
            LanguageService.GetString("Logout"), LanguageService.GetString("Cancel"));
        if (confirm)
        {
            AuthService.Logout();
            AppNavigation.SetRootPage(new NavigationPage(new Views.Auth.LoginPage()));
        }
    }

    private void OnLoginClicked(object sender, EventArgs e)
    {
        AuthService.Logout();
        AppNavigation.SetRootPage(new NavigationPage(new Views.Auth.LoginPage()));
    }

    private void OnRegisterClicked(object sender, EventArgs e)
    {
        AuthService.Logout();
        AppNavigation.SetRootPage(new NavigationPage(new Views.Auth.SignUpPage()));
    }
}

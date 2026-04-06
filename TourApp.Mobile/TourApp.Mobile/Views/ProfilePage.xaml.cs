using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class ProfilePage : ContentPage
{
    public ProfilePage()
    {
        InitializeComponent();
    }

    private async void OnLanguageTapped(object sender, EventArgs e)
    {
        string[] languages = { "Tiếng Việt (vi)", "English (en)", "Français (fr)", "日本語 (ja)", "中文 (zh-CN)" };
        var action = await DisplayActionSheet("Chọn Ngôn Ngữ App", "Huỷ", null, languages);
        if (!string.IsNullOrEmpty(action) && action != "Huỷ")
        {
            var code = action.Split('(')[1].TrimEnd(')');
            Preferences.Default.Set("app_lang", code);
            
            // Apply language to geofence service dynamically
            var geofence = IPlatformApplication.Current?.Services.GetService<GeofenceService>();
            if (geofence != null) geofence.CurrentLanguage = code;

            await DisplayAlert("Ngôn ngữ", $"Đã thay đổi sang: {action}", "OK");
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
            await Shell.Current.GoToAsync("///LoginPage");
        }
    }
}

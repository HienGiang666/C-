using System;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class AppLockPage : ContentPage
{
    private string _currentInput = "";
    private string _expectedPin = "";
    private string _setupPin = "";
    
    // Modes:
    // 0 = Unlock Mode (app start)
    // 1 = Setup Mode (enter new pin)
    // 2 = Setup Mode (confirm new pin)
    // 3 = Disable Mode (enter pin to disable)
    private int _mode = 0;
    
    // Action to call after successful unlock or setup
    public Action OnSuccess { get; set; }

    public AppLockPage(int mode = 0)
    {
        InitializeComponent();
        _mode = mode;
        
        if (_mode == 0 || _mode == 3)
        {
            _expectedPin = Preferences.Default.Get("app_lock_pin", "");
            InstructionLabel.Text = _mode == 0 ? "Nhập mã PIN để mở khóa" : "Nhập mã PIN hiện tại để tắt khóa";
            CancelBtn.IsVisible = _mode == 3;
        }
        else if (_mode == 1)
        {
            InstructionLabel.Text = "Tạo mã PIN mới (4 số)";
            CancelBtn.IsVisible = true;
        }
    }

    private void UpdateDots()
    {
        int length = _currentInput.Length;
        Dot1.BackgroundColor = length >= 1 ? Color.FromArgb("#FF3D00") : Colors.Transparent;
        Dot2.BackgroundColor = length >= 2 ? Color.FromArgb("#FF3D00") : Colors.Transparent;
        Dot3.BackgroundColor = length >= 3 ? Color.FromArgb("#FF3D00") : Colors.Transparent;
        Dot4.BackgroundColor = length >= 4 ? Color.FromArgb("#FF3D00") : Colors.Transparent;
    }

    private async void OnDigitClicked(object sender, EventArgs e)
    {
        if (_currentInput.Length >= 4) return;
        
        var btn = (Button)sender;
        _currentInput += btn.Text;
        UpdateDots();

        if (_currentInput.Length == 4)
        {
            await Task.Delay(200); // Give user time to see the 4th dot fill
            await ProcessPin();
        }
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_currentInput.Length > 0)
        {
            _currentInput = _currentInput.Substring(0, _currentInput.Length - 1);
            UpdateDots();
        }
    }
    
    private void OnCancelClicked(object sender, EventArgs e)
    {
        if (_mode == 0) return; // Cannot cancel unlock
        Navigation.PopModalAsync();
    }

    private async Task ProcessPin()
    {
        if (_mode == 0) // Unlock
        {
            if (_currentInput == _expectedPin)
            {
                // App Unlocked
                if (OnSuccess != null) OnSuccess.Invoke();
                else AppNavigation.SetRootPage(new AppShell());
            }
            else
            {
                await DisplayAlert("Lỗi", "Mã PIN không chính xác", "Thử lại");
                ResetInput();
            }
        }
        else if (_mode == 1) // Setup - Step 1
        {
            _setupPin = _currentInput;
            _mode = 2;
            InstructionLabel.Text = "Nhập lại mã PIN để xác nhận";
            ResetInput();
        }
        else if (_mode == 2) // Setup - Step 2
        {
            if (_currentInput == _setupPin)
            {
                Preferences.Default.Set("app_lock_pin", _setupPin);
                await DisplayAlert("Thành công", "Đã cài đặt khóa ứng dụng", "OK");
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("Lỗi", "Mã PIN không khớp", "Làm lại từ đầu");
                _mode = 1;
                InstructionLabel.Text = "Tạo mã PIN mới (4 số)";
                ResetInput();
            }
        }
        else if (_mode == 3) // Disable mode
        {
            if (_currentInput == _expectedPin)
            {
                Preferences.Default.Remove("app_lock_pin");
                await DisplayAlert("Thành công", "Đã tắt khóa ứng dụng", "OK");
                await Navigation.PopModalAsync();
            }
            else
            {
                await DisplayAlert("Lỗi", "Mã PIN không chính xác", "Thử lại");
                ResetInput();
            }
        }
    }

    private void ResetInput()
    {
        _currentInput = "";
        UpdateDots();
    }
}

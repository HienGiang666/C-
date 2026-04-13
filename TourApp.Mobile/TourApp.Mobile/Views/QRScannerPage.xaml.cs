using System.Diagnostics;
using TourApp.Mobile.Services;
using ZXing.Net.Maui;

namespace TourApp.Mobile.Views;

public partial class QRScannerPage : ContentPage
{
    private bool _isFlashOn = false;
    private bool _isProcessing = false;

    public QRScannerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Hide emulator UI on physical devices
        if (Microsoft.Maui.Devices.DeviceInfo.Current.DeviceType == DeviceType.Physical && DeviceInfo.Platform != DevicePlatform.WinUI)
        {
            EmulatorUI.IsVisible = false;
        }

        // Start scanning
        cameraBarcodeReader.IsDetecting = true;
        
        // Start scan line animation
        StartScanLineAnimation();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        
        // Stop scanning
        cameraBarcodeReader.IsDetecting = false;
    }

    private void StartScanLineAnimation()
    {
        // Simple animation for the scanning line
        var animation = new Animation(v => ScanLine.TranslationY = v, 0, 200);
        animation.Commit(this, "ScanLineAnimation", 16, 2000, Easing.Linear, (v, c) =>
        {
            if (!c)
            {
                // Repeat animation
                MainThread.BeginInvokeOnMainThread(() => StartScanLineAnimation());
            }
        }, () => true);
    }

    private async void OnBarcodesDetected(object sender, ZXing.Net.Maui.BarcodeDetectionEventArgs e)
    {
        if (_isProcessing) return;

        var barcode = e.Results?.FirstOrDefault();
        if (barcode == null) return;

        _isProcessing = true;

        try
        {
            // Parse the QR code value
            var qrValue = barcode.Value;
            Debug.WriteLine($"[QRScanner] Detected: {qrValue}");

            // Stop detecting
            cameraBarcodeReader.IsDetecting = false;

            // Parse POI ID from QR
            int poiId = ParsePoiIdFromQr(qrValue);
            
            if (poiId > 0)
            {
                // Vibrate for feedback
                Vibrate();
                
                // Navigate to MapPage with the POI ID
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync($"//MapPage?poiId={poiId}");
                });
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("Lỗi", "Mã QR không hợp lệ. Vui lòng thử lại.", "OK");
                    cameraBarcodeReader.IsDetecting = true;
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QRScanner] Error: {ex.Message}");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private static int ParsePoiIdFromQr(string qrText)
    {
        // Format: tourapp://poi/3
        if (qrText.StartsWith("tourapp://poi/", StringComparison.OrdinalIgnoreCase))
        {
            var idStr = qrText.Replace("tourapp://poi/", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (int.TryParse(idStr, out int id)) return id;
        }
        
        // Format: chỉ số: "3"
        if (int.TryParse(qrText.Trim(), out int directId)) return directId;
        
        // Format: URL có ?id=3
        try
        {
            var uri = new Uri(qrText);
            var q = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (int.TryParse(q["id"], out int qId)) return qId;
        }
        catch { }
        
        return -1;
    }

    private void Vibrate()
    {
        try
        {
            Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(300));
        }
        catch { }
    }

    private async void OnBackTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void OnFlashTapped(object sender, EventArgs e)
    {
        _isFlashOn = !_isFlashOn;
        cameraBarcodeReader.IsTorchOn = _isFlashOn;
    }

    private async void OnMockScan1Clicked(object sender, EventArgs e)
    {
        Vibrate();
        await Shell.Current.GoToAsync("//MapPage?poiId=1"); // Ốc Xiên Quán
    }

    private async void OnMockScan2Clicked(object sender, EventArgs e)
    {
        Vibrate();
        await Shell.Current.GoToAsync("//MapPage?poiId=3"); // Lẩu Bò
    }
}

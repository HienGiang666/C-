using System.Diagnostics;
using TourApp.Mobile.Services;
#if ANDROID
using Android.Content;
using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using MauiApplication = Microsoft.Maui.Controls.Application;
#endif

namespace TourApp.Mobile.Views;

public partial class QRScannerPage : ContentPage
{
    private bool _isFlashOn = false;
    private bool _isProcessing = false;
    private object? _cameraReader = null;
    private bool _hasPermission = false;

    private bool SupportsCameraScan => DeviceInfo.Platform != DevicePlatform.WinUI;

    public QRScannerPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (SupportsCameraScan && DeviceInfo.DeviceType == DeviceType.Physical)
        {
            // Real device - request permission and init camera
            _ = RequestCameraPermissionAsync();
        }
        else
        {
            // Windows or Emulator - camera not available
            FlashButton.IsVisible = false;
            CameraContainer.IsVisible = false;
        }

        StartScanLineAnimation();
    }

    private async Task RequestCameraPermissionAsync()
    {
#if ANDROID
        var activity = MauiApplication.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Android.App.Activity;
        if (activity == null)
        {
            CameraContainer.IsVisible = false;
            return;
        }

        const string cameraPermission = Android.Manifest.Permission.Camera;

        if (ContextCompat.CheckSelfPermission(activity, cameraPermission) == Permission.Granted)
        {
            _hasPermission = true;
            await MainThread.InvokeOnMainThreadAsync(() => InitializeCamera());
        }
        else
        {
            // Request permission
            ActivityCompat.RequestPermissions(activity, new[] { cameraPermission }, 100);
            // Wait a bit and check again
            await Task.Delay(500);
            if (ContextCompat.CheckSelfPermission(activity, cameraPermission) == Permission.Granted)
            {
                _hasPermission = true;
                await MainThread.InvokeOnMainThreadAsync(() => InitializeCamera());
            }
            else
            {
                CameraContainer.IsVisible = false;
                await DisplayAlert(LanguageService.GetString("Error"), "Cần quyền camera để quét QR", LanguageService.GetString("OK"));
            }
        }
#else
        _hasPermission = true;
        InitializeCamera();
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        StopCamera();
    }

    private void InitializeCamera()
    {
#if ANDROID || IOS
        try
        {
            Debug.WriteLine("[QRScanner] Initializing camera...");

            // Try multiple ways to get the ZXing camera type
            Type? cameraType = null;

            // Method 1: Direct type name
            cameraType = Type.GetType("ZXing.Net.Maui.Controls.CameraBarcodeReaderView, ZXing.Net.Maui.Controls");

            // Method 2: Search in loaded assemblies
            if (cameraType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        cameraType = asm.GetType("ZXing.Net.Maui.Controls.CameraBarcodeReaderView");
                        if (cameraType != null)
                        {
                            Debug.WriteLine($"[QRScanner] Found camera type in assembly: {asm.FullName}");
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (cameraType == null)
            {
                Debug.WriteLine("[QRScanner] ERROR: CameraBarcodeReaderView type not found in any assembly!");
                CameraContainer.IsVisible = false;
                return;
            }

            Debug.WriteLine("[QRScanner] Camera type found, creating instance...");
            _cameraReader = Activator.CreateInstance(cameraType);

            if (_cameraReader == null)
            {
                Debug.WriteLine("[QRScanner] ERROR: Failed to create camera instance!");
                CameraContainer.IsVisible = false;
                return;
            }

            // Get enum type for CameraLocation
            var locationType = Type.GetType("ZXing.Net.Maui.CameraLocation, ZXing.Net.Maui");
            if (locationType != null)
            {
                var rearValue = Enum.Parse(locationType, "Rear");
                cameraType.GetProperty("CameraLocation")?.SetValue(_cameraReader, rearValue);
            }

            // Set other properties
            cameraType.GetProperty("IsDetecting")?.SetValue(_cameraReader, true);
            cameraType.GetProperty("HorizontalOptions")?.SetValue(_cameraReader, LayoutOptions.Fill);
            cameraType.GetProperty("VerticalOptions")?.SetValue(_cameraReader, LayoutOptions.Fill);

            // Hook up event handler
            var eventInfo = cameraType.GetEvent("BarcodesDetected");
            if (eventInfo != null)
            {
                var handlerMethod = this.GetType().GetMethod("OnBarcodesDetectedInternal");
                if (handlerMethod != null)
                {
                    var handler = Delegate.CreateDelegate(eventInfo.EventHandlerType!, this, handlerMethod);
                    eventInfo.AddEventHandler(_cameraReader, handler);
                    Debug.WriteLine("[QRScanner] Event handler attached");
                }
            }

            // Add to container
            if (_cameraReader is View cameraView)
            {
                Debug.WriteLine("[QRScanner] Adding camera to container...");
                CameraContainer.Children.Clear();
                CameraContainer.Children.Add(cameraView);
                CameraContainer.IsVisible = true;
                CameraContainer.BackgroundColor = Colors.Transparent;

                FlashButton.IsVisible = true;

                Debug.WriteLine("[QRScanner] Camera initialized successfully!");
            }
            else
            {
                Debug.WriteLine("[QRScanner] ERROR: Camera reader is not a View!");
                CameraContainer.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QRScanner] Failed to initialize camera: {ex}");
            CameraContainer.IsVisible = false;
        }
#endif
    }

    private void StopCamera()
    {
#if ANDROID || IOS
        try
        {
            if (_cameraReader != null)
            {
                var cameraType = _cameraReader.GetType();
                cameraType.GetProperty("IsDetecting")?.SetValue(_cameraReader, false);
                _cameraReader = null;
            }
        }
        catch { }
#endif
    }

    private void StartScanLineAnimation()
    {
        var animation = new Animation(v => ScanLine.TranslationY = v, 0, 200);
        animation.Commit(this, "ScanLineAnimation", 16, 2000, Easing.Linear, (v, c) =>
        {
            if (!c)
            {
                MainThread.BeginInvokeOnMainThread(() => StartScanLineAnimation());
            }
        }, () => true);
    }

#if ANDROID || IOS
    // Event handler called via reflection on mobile platforms
    public void OnBarcodesDetectedInternal(object? sender, object e)
    {
        if (_isProcessing) return;

        try
        {
            // Use reflection to get results from event args
            var resultsProperty = e.GetType().GetProperty("Results");
            var results = resultsProperty?.GetValue(e) as System.Collections.IEnumerable;

            var barcode = results?.Cast<object>().FirstOrDefault();
            if (barcode == null) return;

            var valueProperty = barcode.GetType().GetProperty("Value");
            var qrValue = valueProperty?.GetValue(barcode)?.ToString();

            if (string.IsNullOrEmpty(qrValue)) return;

            _isProcessing = true;
            Debug.WriteLine($"[QRScanner] Detected: {qrValue}");

            StopCamera();

            int poiId = ParsePoiIdFromQr(qrValue);

            if (poiId > 0)
            {
                Vibrate();
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Shell.Current.GoToAsync($"//MapPage?poiId={poiId}&fromQR=true");
                });
            }
            else
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("QRInvalid"), LanguageService.GetString("OK"));
                    _isProcessing = false;
                    // Restart camera
                    var cameraType = _cameraReader?.GetType();
                    cameraType?.GetProperty("IsDetecting")?.SetValue(_cameraReader, true);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[QRScanner] Error: {ex.Message}");
            _isProcessing = false;
        }
    }
#endif

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
#if ANDROID || IOS
        _isFlashOn = !_isFlashOn;
        try
        {
            if (_cameraReader != null)
            {
                var cameraType = _cameraReader.GetType();
                cameraType.GetProperty("IsTorchOn")?.SetValue(_cameraReader, _isFlashOn);
            }
        }
        catch { }
#endif
    }

}

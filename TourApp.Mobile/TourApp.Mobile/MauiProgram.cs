using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using TourApp.Mobile.Services;
using ZXing.Net.Maui.Controls;

namespace TourApp.Mobile
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            // Cấu hình WebView cho Android trước khi build
#if ANDROID
            Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("GoongWebView", (handler, view) =>
            {
                if (handler.PlatformView != null && handler.PlatformView.Settings != null)
                {
                    handler.PlatformView.Settings.JavaScriptEnabled = true;
                    handler.PlatformView.Settings.DomStorageEnabled = true;
                    handler.PlatformView.Settings.MixedContentMode = Android.Webkit.MixedContentHandling.AlwaysAllow;
                    // Bỏ ép phần cứng (Hardware Layer) vì gây crash native (văng app tắp lự) trên Android 9 (Oppo A31)
                    // handler.PlatformView.SetLayerType(Android.Views.LayerType.Hardware, null);
                }
            });

            // ===== FIX: Vietnamese Telex/VNI input bị mất chữ khi gõ dấu =====
            // Nguyên nhân: MAUI update Text property liên tục làm hỏng composition state của Android IME.
            // Fix 1: SetRawInputType để giữ composition state.
            // Fix 2: Thêm ImeOptions.NoExtractUi, tắt TextFlagNoSuggestions.
            // Fix 3: Đặt PrivateImeOptions "nm" (no microphone) để giảm interference.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("VietnameseInputFix", (handler, view) =>
            {
                if (handler.PlatformView is Android.Widget.EditText editText)
                {
                    if (view is Entry entry && !entry.IsPassword)
                    {
                        editText.SetRawInputType(Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationNormal);
                        editText.ImeOptions = Android.Views.InputMethods.ImeAction.Done;
                        editText.PrivateImeOptions = "nm";
                    }
                }
            });

            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("VietnameseEditorFix", (handler, view) =>
            {
                if (handler.PlatformView is Android.Widget.EditText editText)
                {
                    editText.SetRawInputType(Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationNormal | Android.Text.InputTypes.TextFlagMultiLine);
                    editText.ImeOptions = Android.Views.InputMethods.ImeAction.Unspecified;
                    editText.PrivateImeOptions = "nm";
                }
            });

            Microsoft.Maui.Handlers.SearchBarHandler.Mapper.AppendToMapping("VietnameseSearchFix", (handler, view) =>
            {
                if (handler.PlatformView is AndroidX.AppCompat.Widget.SearchView searchView)
                {
                    var editText = FindEditText(searchView);
                    if (editText != null)
                    {
                        editText.SetRawInputType(Android.Text.InputTypes.ClassText | Android.Text.InputTypes.TextVariationNormal);
                        editText.PrivateImeOptions = "nm";
                    }
                }
            });
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseBarcodeReader()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<ApiService>();
            builder.Services.AddSingleton<LocationService>();
            builder.Services.AddSingleton<GeofenceService>();
            
            builder.Services.AddTransient<Views.MapPage>();
            builder.Services.AddTransient<Views.HomePage>();
            builder.Services.AddTransient<Views.POIPage>();
            builder.Services.AddTransient<Views.TourPage>();
            builder.Services.AddTransient<Views.ProfilePage>();
            
            builder.Services.AddTransient<Views.Auth.LoginPage>();
            builder.Services.AddTransient<Views.Auth.SignUpPage>();
            builder.Services.AddTransient<Views.Auth.ForgotPasswordPage>();
            builder.Services.AddTransient<Views.Auth.VerificationPage>();

            // Initialize LanguageService for localization
            LanguageService.Initialize();

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }

#if ANDROID
        /// <summary>
        /// Tìm EditText bên trong ViewGroup (dùng cho SearchView)
        /// </summary>
        private static Android.Widget.EditText? FindEditText(Android.Views.ViewGroup viewGroup)
        {
            for (int i = 0; i < viewGroup.ChildCount; i++)
            {
                var child = viewGroup.GetChildAt(i);
                if (child is Android.Widget.EditText editText)
                    return editText;
                if (child is Android.Views.ViewGroup childGroup)
                {
                    var result = FindEditText(childGroup);
                    if (result != null) return result;
                }
            }
            return null;
        }
#endif
    }
}
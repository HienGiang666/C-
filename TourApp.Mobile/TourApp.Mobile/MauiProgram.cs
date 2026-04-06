using Microsoft.Extensions.Logging;
using Microsoft.Maui.Handlers;
using TourApp.Mobile.Services;

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
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
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

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
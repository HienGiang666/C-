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
                    handler.PlatformView.SetLayerType(Android.Views.LayerType.Hardware, null);
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

#if DEBUG
            builder.Logging.AddDebug();
#endif
            return builder.Build();
        }
    }
}
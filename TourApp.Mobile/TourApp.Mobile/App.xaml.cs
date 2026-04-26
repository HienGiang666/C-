using TourApp.Mobile.Services;

namespace TourApp.Mobile
{
    public partial class App : Microsoft.Maui.Controls.Application
    {
        public App()
        {
            // ── Global unhandled exception handlers ───────────────────────────
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"[CRASH][AppDomain] {ex}");
                // NOTE: AppDomain.UnhandledException là notification-only — app vẫn crash sau đây.
                // Để xem lỗi: check Output > Debug trong VS, hoặc Android logcat.

            };

            TaskScheduler.UnobservedTaskException += (_, args) =>
            {
                // [QUAN TRỌNG] SetObserved() ngăn app crash do task exception không được await
                // Đây là catch-all cho mọi _ = Task.Run(...) throw exception
                System.Diagnostics.Debug.WriteLine($"[CRASH][UnobservedTask] {args.Exception}");
                args.SetObserved();
            };

            // ─────────────────────────────────────────────────────────────────
            InitializeComponent();

            _ = Task.Run(async () =>
            {
                try
                {
                    await ApiService.AutoDiscoverApiAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] API auto-discovery failed: {ex.Message}");
                }
            });
        }



        protected override Window CreateWindow(IActivationState? activationState)
        {
            AuthService.LoadSavedSession();

            Page startPage;
            if (AuthService.IsLoggedIn && !string.IsNullOrEmpty(AuthService.CurrentUser?.Username))
            {
                startPage = new AppShell();
                // Bắt đầu session tracking ngay vì đã đăng nhập
                UserSessionService.StartSession(AuthService.CurrentUser!.Id, AuthService.CurrentUser!.FullName ?? AuthService.CurrentUser!.Username);
            }
            else if (Preferences.Get("is_guest_user", false))
            {
                // Khách đăng nhập - cũng cần start session
                var guestName = Preferences.Get("guest_name", "Khách");
                var guestId = Preferences.Get("guest_id", "");
                // Tạo mới nếu chưa có (đảm bảo mỗi máy có ID duy nhất)
                if (string.IsNullOrEmpty(guestId))
                {
                    guestId = $"guest_{Guid.NewGuid().ToString("N")[..8]}";
                    Preferences.Set("guest_id", guestId);
                }
                startPage = new AppShell();
                UserSessionService.StartSession(null, guestName, guestId);
            }
            else
            {
                startPage = new NavigationPage(new Views.Auth.LoginPage());
            }

            var window = new Window(startPage);
            window.Destroying += async (_, __) =>
            {
                try
                {
                    await UserSessionService.StopSessionAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[App] Window.Destroying error: {ex.Message}");
                }
            };

            return window;
        }



        /// <summary>
        /// App đi vào background / bị tắt → báo offline
        /// </summary>
        protected override void OnSleep()
        {
            base.OnSleep();
            System.Diagnostics.Debug.WriteLine("[App] OnSleep - App going to background, sending logout");

            // Báo server là user đã offline
            _ = UserSessionService.StopSessionAsync();
        }



        /// <summary>
        /// App quay lại foreground → bắt đầu session lại
        /// </summary>
        protected override void OnResume()
        {
            base.OnResume();
            System.Diagnostics.Debug.WriteLine("[App] OnResume - App back to foreground");

            // Restart session tracking nếu đang đăng nhập
            if (AuthService.IsLoggedIn && AuthService.CurrentUser != null)
            {
                UserSessionService.StartSession(AuthService.CurrentUser.Id, AuthService.CurrentUser.FullName ?? AuthService.CurrentUser.Username);
            }
            else if (Preferences.Get("is_guest_user", false))
            {
                var guestName = Preferences.Get("guest_name", "Khách");
                var guestId = Preferences.Get("guest_id", "");
                if (string.IsNullOrEmpty(guestId))
                {
                    guestId = $"guest_{Guid.NewGuid().ToString("N")[..8]}";
                    Preferences.Set("guest_id", guestId);
                }
                UserSessionService.StartSession(null, guestName, guestId);
            }
        }



        /// <summary>
        /// Xử lý deep link khi quét QR mở app
        /// tourapp://poi/{id}
        /// </summary>
        protected override void OnAppLinkRequestReceived(Uri uri)
        {
            base.OnAppLinkRequestReceived(uri);
            System.Diagnostics.Debug.WriteLine($"[DeepLink] Received: {uri}");

            // Parse tourapp://poi/{id}
            if (uri.Scheme == "tourapp" && uri.Host == "poi")
            {
                var path = uri.AbsolutePath.Trim('/');
                if (int.TryParse(path, out var poiId))
                {
                    System.Diagnostics.Debug.WriteLine($"[DeepLink] Navigating to POI {poiId}");

                    // Delay một chút để app khởi động xong
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await Task.Delay(500);
                        await HandleDeepLinkToPoi(poiId);
                    });
                }
            }
        }



        private async Task HandleDeepLinkToPoi(int poiId)
        {
            try
            {
                // Kiểm tra đã đăng nhập chưa
                if (!AuthService.IsLoggedIn && !Preferences.Get("is_guest_user", false))
                {
                    // Lưu POI ID để mở sau khi đăng nhập
                    Preferences.Set("pending_poi_id", poiId);
                    System.Diagnostics.Debug.WriteLine("[DeepLink] User not logged in, saved pending POI ID");
                    return;
                }

                // Navigate đến POI detail page
                if (Shell.Current != null)
                {
                    await Shell.Current.GoToAsync($"//MapPage?poiId={poiId}&fromQR=true");
                    System.Diagnostics.Debug.WriteLine($"[DeepLink] Navigated to POI {poiId}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DeepLink] Shell.Current is null, cannot navigate");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DeepLink] Error: {ex.Message}");
            }
        }
    }
}

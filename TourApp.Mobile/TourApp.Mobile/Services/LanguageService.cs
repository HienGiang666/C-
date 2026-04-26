using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml.Linq;
using Microsoft.Maui.Storage;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace TourApp.Mobile.Services
{
    /// <summary>
    /// Service quản lý ngôn ngữ toàn app với RESX localization
    /// Đọc RESX files trực tiếp dạng XML để tránh lỗi ResourceManager trong MAUI
    /// </summary>
    public static class LanguageService
    {
        private const string DefaultLanguage = "vi";
        private static readonly Dictionary<string, Dictionary<string, string>> _resources = new();
        private static bool _isInitialized = false;
        
        /// <summary>
        /// Mã ngôn ngữ hiện tại (vi, en, zh, ja)
        /// </summary>
        public static string CurrentLanguage
        {
            get => Preferences.Default.Get("app_lang", DefaultLanguage);
            set
            {
                var current = CurrentLanguage;
                if (value != current && !string.IsNullOrWhiteSpace(value))
                {
                    var newLang = value.ToLower();
                    Preferences.Default.Set("app_lang", newLang);
                    
                    // Update current culture for RESX
                    SetCulture(newLang);
                    
                    OnLanguageChanged(newLang);
                }
            }
        }
        
        /// <summary>
        /// Khởi tạo language service - đọc tất cả RESX files vào memory
        /// </summary>
        public static void Initialize()
        {
            if (_isInitialized) return;
            
            try
            {
                LoadAllResources();
                var savedLang = CurrentLanguage;
                SetCulture(savedLang);
                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Initialized with language: {savedLang}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Init error: {ex.Message}");
            }
        }

        /// <summary>
        /// Lazy initialization - chỉ set culture, load resources khi cần
        /// </summary>
        public static void InitializeLazy()
        {
            if (_isInitialized) return;
            
            try
            {
                // Chỉ set culture, không load tất cả resources ngay
                var savedLang = CurrentLanguage;
                SetCulture(savedLang);
                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Lazy initialized with language: {savedLang}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Lazy init error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Load tất cả RESX files từ embedded resources
        /// </summary>
        private static void LoadAllResources()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();
            
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Found {resourceNames.Length} embedded resources");
            
            // Tìm tất cả Strings*.resx files
            foreach (var resourceName in resourceNames)
            {
                if (resourceName.Contains("Strings") && resourceName.EndsWith(".resx"))
                {
                    var langCode = ExtractLanguageCode(resourceName);
                    LoadResourceFile(assembly, resourceName, langCode);
                }
            }
            
            // Nếu không tìm thấy resource nào, load từ file system (fallback)
            if (_resources.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[LanguageService] No embedded resources found, trying file system...");
                LoadResourcesFromFileSystem();
            }
        }
        
        /// <summary>
        /// Extract language code từ resource name (e.g., "Strings.en.resx" -> "en")
        /// </summary>
        private static string ExtractLanguageCode(string resourceName)
        {
            // Pattern: TourApp.Mobile.Resources.Strings.en.resx hoặc TourApp.Mobile.Resources.Strings.resx
            var parts = resourceName.Split('.');
            if (parts.Length >= 2)
            {
                var resxName = parts[^2]; // Tên file trước .resx
                if (resxName.StartsWith("Strings"))
                {
                    // Strings.resx -> default (vi)
                    // Strings.en.resx -> en
                    if (resxName == "Strings") return DefaultLanguage;
                    
                    // Strings.en.resx hoặc chỉ "en"
                    var langPart = resxName.Replace("Strings", "").Trim('.');
                    if (!string.IsNullOrEmpty(langPart))
                        return langPart.ToLower();
                }
            }
            return DefaultLanguage;
        }
        
        /// <summary>
        /// Load resource file từ assembly
        /// </summary>
        private static void LoadResourceFile(Assembly assembly, string resourceName, string langCode)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return;
                
                var doc = XDocument.Load(stream);
                var dict = new Dictionary<string, string>();
                
                foreach (var dataElement in doc.Descendants("data"))
                {
                    var key = dataElement.Attribute("name")?.Value;
                    var value = dataElement.Element("value")?.Value;
                    
                    if (!string.IsNullOrEmpty(key) && value != null)
                    {
                        dict[key] = value;
                    }
                }
                
                _resources[langCode] = dict;
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Loaded {dict.Count} keys for '{langCode}' from {resourceName}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to load {resourceName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Fallback: Load từ file system nếu embedded resources không có
        /// </summary>
        private static void LoadResourcesFromFileSystem()
        {
            try
            {
                var resourcesDir = Path.Combine(FileSystem.AppDataDirectory, "..", "Resources");
                if (!Directory.Exists(resourcesDir))
                    resourcesDir = Path.Combine(AppContext.BaseDirectory, "Resources");
                
                if (!Directory.Exists(resourcesDir))
                {
                    // Tạo built-in fallback
                    CreateBuiltInFallback();
                    return;
                }
                
                var resxFiles = Directory.GetFiles(resourcesDir, "Strings*.resx");
                foreach (var file in resxFiles)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var langCode = fileName == "Strings" ? DefaultLanguage : fileName.Replace("Strings.", "").ToLower();
                    
                    try
                    {
                        var doc = XDocument.Load(file);
                        var dict = new Dictionary<string, string>();
                        
                        foreach (var dataElement in doc.Descendants("data"))
                        {
                            var key = dataElement.Attribute("name")?.Value;
                            var value = dataElement.Element("value")?.Value;
                            
                            if (!string.IsNullOrEmpty(key) && value != null)
                            {
                                dict[key] = value;
                            }
                        }
                        
                        _resources[langCode] = dict;
                        System.Diagnostics.Debug.WriteLine($"[LanguageService] Loaded {dict.Count} keys for '{langCode}' from file");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to load {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] File system load error: {ex.Message}");
                CreateBuiltInFallback();
            }
        }
        
        /// <summary>
        /// Tạo built-in fallback nếu không load được resource files
        /// </summary>
        private static void CreateBuiltInFallback()
        {
            var fallbackDict = new Dictionary<string, string>
            {
                ["Error"] = "Lỗi", ["Success"] = "Thành công", ["OK"] = "OK", ["Cancel"] = "Hủy", ["Loading"] = "Đang tải...",
                ["EmailRequired"] = "Vui lòng nhập email", ["ResetCodeRequired"] = "Vui lòng nhập mã reset", ["NewPasswordRequired"] = "Vui lòng nhập mật khẩu mới",
                ["UsernamePlaceholder"] = "Vui lòng nhập tài khoản", ["PasswordPlaceholder"] = "Vui lòng nhập mật khẩu",
                ["PasswordTooShort"] = "Mật khẩu phải có ít nhất 6 ký tự", ["PasswordMismatch"] = "Mật khẩu xác nhận không khớp", ["ServerError"] = "Lỗi kết nối",
                ["SendCode"] = "Gửi mã", ["ResetPasswordButton"] = "Đặt lại mật khẩu",
                ["Login"] = "Đăng nhập", ["LoginButton"] = "Đăng nhập", ["SignUp"] = "Đăng ký", ["RegisterButton"] = "Đăng ký", ["ForgotPassword"] = "Quên mật khẩu?", ["GuestLogin"] = "Đăng nhập khách", ["Email"] = "Email", ["Password"] = "Mật khẩu",
                ["Home"] = "Trang chủ", ["Tours"] = "Tour", ["Map"] = "Bản đồ", ["Profile"] = "Hồ sơ",
                ["TabHome"] = "Trang chủ", ["TabFood"] = "Quán ăn", ["TabMap"] = "Bản đồ", ["TabTour"] = "Tour", ["TabProfile"] = "Tôi",
                ["Welcome"] = "Chào", ["SelectLanguage"] = "Chọn ngôn ngữ", ["Settings"] = "Cài đặt khóa ứng dụng", ["MyTours"] = "Các Tour Đã Đặt", ["ChangePassword"] = "Đổi mật khẩu", ["Logout"] = "Đăng xuất",
                ["SearchPlaceholder"] = "Tìm món, tìm quán, tìm tour...", ["CategoryNuong"] = "Nướng", ["CategoryLau"] = "Lẩu", ["CategoryOc"] = "Ốc", ["CategoryAnVat"] = "Ăn vặt",
                ["SuggestedTours"] = "🔥 Gợi ý Lộ trình", ["SeeMore"] = "Xem thêm >", ["TopPlaces"] = "Top Nổi Bật",
                ["SearchFood"] = "Tìm quán ăn...", ["DiscoverTours"] = "Khám Phá Tour", ["SearchTour"] = "Tìm tour...", ["StartTour"] = "Bắt đầu Tour",
                ["SearchMap"] = "Tìm quán ăn, điểm tham quan...", ["Search"] = "Tìm", ["ListenAudio"] = "🔊 Nghe", ["Directions"] = "🗺️ Đường đi", ["Close"] = "❌ Đóng",
                ["TourDetailTitle"] = "Chi tiết Tour", ["Introduction"] = "Giới thiệu", ["Destinations"] = "Các điểm đến", ["DownloadOfflineAudio"] = "⬇️ Tải Audio Offline", ["ViewMap"] = "Xem Bản Đồ", ["BookTour"] = "Đặt Tour",
                // ChangePasswordPage keys
                ["CurrentPassword"] = "Mật khẩu hiện tại", ["CurrentPasswordPlaceholder"] = "Nhập mật khẩu hiện tại",
                ["NewPassword"] = "Mật khẩu mới", ["NewPasswordPlaceholder"] = "Nhập mật khẩu mới (tối thiểu 6 ký tự)",
                ["ConfirmNewPassword"] = "Xác nhận mật khẩu mới", ["ConfirmPasswordPlaceholder"] = "Nhập lại mật khẩu mới",
                ["ChangePasswordButton"] = "Đổi mật khẩu",
                // UserBookingsPage keys
                ["NoBookings"] = "Bạn chưa đặt tour nào.",
                ["TourDate"] = "Ngày đi: ", ["Participants"] = "Số người: ", ["TotalPrice"] = "Tổng tiền: ",
                ["Status"] = "Trạng thái: ", ["BookingDate"] = "Đặt ngày",
                // QR Scanner keys
                ["QRScannerTitle"] = "Quét mã QR", ["ScanQRInstruction"] = "Đưa mã QR vào khung để quét",
                ["QRHelpText"] = "Mã QR thường được dán tại các điểm tham quan",
                ["QRInvalid"] = "Mã QR không hợp lệ. Vui lòng thử lại."
            };
            
            var enDict = new Dictionary<string, string>
            {
                ["Error"] = "Error", ["Success"] = "Success", ["OK"] = "OK", ["Cancel"] = "Cancel", ["Loading"] = "Loading...",
                ["EmailRequired"] = "Please enter email", ["ResetCodeRequired"] = "Please enter reset code", ["NewPasswordRequired"] = "Please enter new password",
                ["UsernamePlaceholder"] = "Please enter username", ["PasswordPlaceholder"] = "Please enter password",
                ["PasswordTooShort"] = "Password must be at least 6 characters", ["PasswordMismatch"] = "Passwords do not match", ["ServerError"] = "Connection Error",
                ["SendCode"] = "Send Code", ["ResetPasswordButton"] = "Reset Password",
                ["Login"] = "Login", ["LoginButton"] = "Login", ["SignUp"] = "Sign Up", ["RegisterButton"] = "Sign Up", ["ForgotPassword"] = "Forgot Password?", ["GuestLogin"] = "Guest Login", ["Email"] = "Email", ["Password"] = "Password",
                ["Home"] = "Home", ["Tours"] = "Tours", ["Map"] = "Map", ["Profile"] = "Profile",
                ["TabHome"] = "Home", ["TabFood"] = "Food", ["TabMap"] = "Map", ["TabTour"] = "Tour", ["TabProfile"] = "Me",
                ["Welcome"] = "Hello", ["SelectLanguage"] = "Select Language", ["Settings"] = "App Lock Setting", ["MyTours"] = "My Tours", ["ChangePassword"] = "Change Password", ["Logout"] = "Logout",
                ["SearchPlaceholder"] = "Search food, places, tours...", ["CategoryNuong"] = "BBQ", ["CategoryLau"] = "Hotpot", ["CategoryOc"] = "Snails", ["CategoryAnVat"] = "Snacks",
                ["SuggestedTours"] = "🔥 Suggested Tours", ["SeeMore"] = "See more >", ["TopPlaces"] = "Top Rated",
                ["SearchFood"] = "Search food...", ["DiscoverTours"] = "Discover Tours", ["SearchTour"] = "Search tours...", ["StartTour"] = "Start Tour",
                ["SearchMap"] = "Search places, tours...", ["Search"] = "Search", ["ListenAudio"] = "🔊 Listen", ["Directions"] = "🗺️ Directions", ["Close"] = "❌ Close",
                ["TourDetailTitle"] = "Tour Details", ["Introduction"] = "Introduction", ["Destinations"] = "Destinations", ["DownloadOfflineAudio"] = "⬇️ Download Offline Audio", ["ViewMap"] = "View Map", ["BookTour"] = "Book Tour",
                // ChangePasswordPage keys
                ["CurrentPassword"] = "Current Password", ["CurrentPasswordPlaceholder"] = "Enter current password",
                ["NewPassword"] = "New Password", ["NewPasswordPlaceholder"] = "Enter new password (min 6 chars)",
                ["ConfirmNewPassword"] = "Confirm New Password", ["ConfirmPasswordPlaceholder"] = "Re-enter new password",
                ["ChangePasswordButton"] = "Change Password",
                // UserBookingsPage keys
                ["NoBookings"] = "You haven't booked any tours yet.",
                ["TourDate"] = "Tour Date: ", ["Participants"] = "Participants: ", ["TotalPrice"] = "Total: ",
                ["Status"] = "Status: ", ["BookingDate"] = "Booked on",
                // QR Scanner keys
                ["QRScannerTitle"] = "Scan QR Code", ["ScanQRInstruction"] = "Point QR code at the frame to scan",
                ["QRHelpText"] = "QR codes are usually placed at tourist attractions",
                ["QRInvalid"] = "Invalid QR code. Please try again."
            };

            var zhDict = new Dictionary<string, string>(enDict)
            {
                ["Error"] = "错误", ["Success"] = "成功", ["OK"] = "确定", ["Cancel"] = "取消", ["Loading"] = "加载中...",
                ["EmailRequired"] = "请输入邮箱", ["ResetCodeRequired"] = "请输入重置码", ["NewPasswordRequired"] = "请输入新密码",
                ["UsernamePlaceholder"] = "请输入用户名", ["PasswordPlaceholder"] = "请输入密码",
                ["PasswordTooShort"] = "密码至少6个字符", ["PasswordMismatch"] = "确认密码不匹配", ["ServerError"] = "连接错误",
                ["SendCode"] = "发送验证码", ["ResetPasswordButton"] = "重置密码",
                ["Login"] = "登录", ["LoginButton"] = "登录", ["SignUp"] = "注册", ["RegisterButton"] = "注册", ["ForgotPassword"] = "忘记密码?", ["GuestLogin"] = "游客登录", ["Email"] = "邮箱", ["Password"] = "密码",
                ["Home"] = "首页", ["Tours"] = "旅游", ["Map"] = "地图", ["Profile"] = "个人",
                ["TabHome"] = "首页", ["TabFood"] = "美食", ["TabMap"] = "地图", ["TabTour"] = "旅游", ["TabProfile"] = "我的",
                ["Welcome"] = "你好", ["SelectLanguage"] = "选择语言", ["Settings"] = "应用锁定设置", ["MyTours"] = "我的旅游", ["ChangePassword"] = "修改密码", ["Logout"] = "退出登录",
                ["SearchPlaceholder"] = "搜索美食、餐厅、旅游...", ["CategoryNuong"] = "烧烤", ["CategoryLau"] = "火锅", ["CategoryOc"] = "海螺", ["CategoryAnVat"] = "小吃",
                ["SuggestedTours"] = "🔥 推荐路线", ["SeeMore"] = "查看更多 >", ["TopPlaces"] = "热门推荐",
                ["SearchFood"] = "搜索餐厅...", ["DiscoverTours"] = "探索旅游", ["SearchTour"] = "搜索旅游...", ["StartTour"] = "开始旅游",
                ["SearchMap"] = "搜索餐厅、景点...", ["Search"] = "搜索", ["ListenAudio"] = "🔊 收听", ["Directions"] = "🗺️ 导航", ["Close"] = "❌ 关闭",
                ["TourDetailTitle"] = "旅游详情", ["Introduction"] = "简介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ 下载离线音频", ["ViewMap"] = "查看地图", ["BookTour"] = "预订旅游",
                // ChangePasswordPage keys
                ["CurrentPassword"] = "当前密码", ["CurrentPasswordPlaceholder"] = "请输入当前密码",
                ["NewPassword"] = "新密码", ["NewPasswordPlaceholder"] = "请输入新密码（至少6个字符）",
                ["ConfirmNewPassword"] = "确认新密码", ["ConfirmPasswordPlaceholder"] = "请再次输入新密码",
                ["ChangePasswordButton"] = "修改密码",
                // UserBookingsPage keys
                ["NoBookings"] = "您还没有预订任何旅游。",
                ["TourDate"] = "出发日期：", ["Participants"] = "人数：", ["TotalPrice"] = "总价：",
                ["Status"] = "状态：", ["BookingDate"] = "预订日期",
                // QR Scanner keys
                ["QRScannerTitle"] = "扫描二维码", ["ScanQRInstruction"] = "将二维码对准框内进行扫描",
                ["QRHelpText"] = "二维码通常贴在旅游景点",
                ["QRInvalid"] = "二维码无效。请重试。"
            };

            var jaDict = new Dictionary<string, string>(enDict)
            {
                ["Error"] = "エラー", ["Success"] = "成功", ["OK"] = "OK", ["Cancel"] = "キャンセル", ["Loading"] = "読み込み中...",
                ["EmailRequired"] = "メールアドレスを入力してください", ["ResetCodeRequired"] = "リセットコードを入力してください", ["NewPasswordRequired"] = "新しいパスワードを入力してください",
                ["UsernamePlaceholder"] = "ユーザー名を入力してください", ["PasswordPlaceholder"] = "パスワードを入力してください",
                ["PasswordTooShort"] = "パスワードは6文字以上", ["PasswordMismatch"] = "確認パスワードが一致しません", ["ServerError"] = "接続エラー",
                ["SendCode"] = "コード送信", ["ResetPasswordButton"] = "パスワードリセット",
                ["Login"] = "ログイン", ["LoginButton"] = "ログイン", ["SignUp"] = "サインアップ", ["RegisterButton"] = "サインアップ", ["ForgotPassword"] = "パスワードを忘れた?", ["GuestLogin"] = "ゲストログイン", ["Email"] = "メール", ["Password"] = "パスワード",
                ["Home"] = "ホーム", ["Tours"] = "ツアー", ["Map"] = "マップ", ["Profile"] = "プロフィール",
                ["TabHome"] = "ホーム", ["TabFood"] = "グルメ", ["TabMap"] = "マップ", ["TabTour"] = "ツアー", ["TabProfile"] = "マイ",
                ["Welcome"] = "こんにちは", ["SelectLanguage"] = "言語を選択", ["Settings"] = "アプリロック設定", ["MyTours"] = "マイツアー", ["ChangePassword"] = "パスワード変更", ["Logout"] = "ログアウト",
                ["SearchPlaceholder"] = "料理、レストラン、ツアーを検索...", ["CategoryNuong"] = "焼肉", ["CategoryLau"] = "鍋", ["CategoryOc"] = "貝", ["CategoryAnVat"] = "軽食",
                ["SuggestedTours"] = "🔥 おすすめルート", ["SeeMore"] = "もっと見る >", ["TopPlaces"] = "人気スポット",
                ["SearchFood"] = "レストラン検索...", ["DiscoverTours"] = "ツアーを探す", ["SearchTour"] = "ツアー検索...", ["StartTour"] = "ツアー開始",
                ["SearchMap"] = "レストラン、観光地を検索...", ["Search"] = "検索", ["ListenAudio"] = "🔊 再生", ["Directions"] = "🗺️ 経路", ["Close"] = "❌ 閉じる",
                ["TourDetailTitle"] = "ツアー詳細", ["Introduction"] = "紹介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ オフライン音声DL", ["ViewMap"] = "地図を見る", ["BookTour"] = "ツアー予約",
                // ChangePasswordPage keys
                ["CurrentPassword"] = "現在のパスワード", ["CurrentPasswordPlaceholder"] = "現在のパスワードを入力",
                ["NewPassword"] = "新しいパスワード", ["NewPasswordPlaceholder"] = "新しいパスワードを入力（6文字以上）",
                ["ConfirmNewPassword"] = "新しいパスワードの確認", ["ConfirmPasswordPlaceholder"] = "新しいパスワードを再入力",
                ["ChangePasswordButton"] = "パスワード変更",
                // UserBookingsPage keys
                ["NoBookings"] = "まだツアーを予約していません。",
                ["TourDate"] = "ツアー日：", ["Participants"] = "参加人数：", ["TotalPrice"] = "合計：",
                ["Status"] = "ステータス：", ["BookingDate"] = "予約日",
                // QR Scanner keys
                ["QRScannerTitle"] = "QRコードをスキャン", ["ScanQRInstruction"] = "QRコードをフレーム内に合わせてスキャン",
                ["QRHelpText"] = "QRコードは通常、観光地に設置されています",
                ["QRInvalid"] = "無効なQRコードです。もう一度お試しください。"
            };

            _resources["vi"] = fallbackDict;
            _resources["en"] = enDict;
            _resources["zh"] = zhDict;
            _resources["ja"] = jaDict;
            
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Created built-in fallback for 4 languages (vi, en, zh, ja)");
        }
        
        /// <summary>
        /// Set culture cho localization
        /// </summary>
        private static void SetCulture(string langCode)
        {
            try
            {
                var culture = new CultureInfo(langCode);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Culture set to: {langCode}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to set culture: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Lấy string đã localize theo key - lazy load resources khi cần
        /// </summary>
        public static string GetString(string key)
        {
            // Lazy init chỉ set culture
            if (!_isInitialized)
                InitializeLazy();

            // Lazy load resources nếu chưa load
            if (_resources.Count == 0)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageService] Lazy loading resources...");
                    LoadAllResources();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to lazy load resources: {ex.Message}");
                }
            }
            
            try
            {
                var lang = CurrentLanguage;
                
                // Thử tìm trong ngôn ngữ hiện tại
                if (_resources.TryGetValue(lang, out var currentDict) && currentDict.TryGetValue(key, out var value))
                    return value;
                
                // Fallback về default language
                if (_resources.TryGetValue(DefaultLanguage, out var defaultDict) && defaultDict.TryGetValue(key, out var defaultValue))
                    return defaultValue;
                
                // Nếu không tìm thấy, trả về key
                return key;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] GetString error for '{key}': {ex.Message}");
                return key;
            }
        }
        
        /// <summary>
        /// Lấy string đã localize với format arguments
        /// </summary>
        public static string GetString(string key, params object[] args)
        {
            var value = GetString(key);
            try
            {
                return string.Format(value, args);
            }
            catch
            {
                return value;
            }
        }
        
        /// <summary>
        /// Sự kiện khi ngôn ngữ thay đổi
        /// </summary>
        public static event EventHandler<string>? LanguageChanged;
        
        private static void OnLanguageChanged(string newLang)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Language changed to: {newLang}");
            
            // Đồng bộ với GeofenceService nếu có instance
            SyncWithGeofenceService(newLang);
            
            // Broadcast event cho tất cả các page đã subscribe
            LanguageChanged?.Invoke(null, newLang);
        }
        
        /// <summary>
        /// Đồng bộ ngôn ngữ với GeofenceService để TTS đúng ngôn ngữ
        /// </summary>
        private static void SyncWithGeofenceService(string lang)
        {
            try
            {
                var geofence = IPlatformApplication.Current?.Services.GetService<GeofenceService>();
                if (geofence != null)
                {
                    geofence.CurrentLanguage = lang;
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Synced GeofenceService language to: {lang}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to sync GeofenceService: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Lấy tên ngôn ngữ hiển thị
        /// </summary>
        public static string GetLanguageName(string code)
        {
            return code?.ToLower() switch
            {
                "vi" => "Tiếng Việt",
                "en" => "English",
                "zh" => "中文",
                "ja" => "日本語",
                _ => code?.ToUpper() ?? "Unknown"
            };
        }
        
        /// <summary>
        /// Danh sách ngôn ngữ hỗ trợ đầy đủ thông tin
        /// </summary>
        public static readonly List<LanguageInfo> SupportedLanguages = new()
        {
            new LanguageInfo { Code = "vi", Name = "Vietnamese", NativeName = "Tiếng Việt", IsActive = true },
            new LanguageInfo { Code = "en", Name = "English", NativeName = "English", IsActive = true },
            new LanguageInfo { Code = "zh", Name = "Chinese", NativeName = "中文", IsActive = true },
            new LanguageInfo { Code = "ja", Name = "Japanese", NativeName = "日本語", IsActive = true }
        };
        
        /// <summary>
        /// Đồng bộ bản dịch UI từ server API.
        /// Gọi khi app khởi động (sau Initialize) để thay thế hardcode bằng dữ liệu từ server.
        /// Nếu không có mạng → sử dụng cache hoặc built-in fallback.
        /// </summary>
        public static async Task SyncFromServerAsync()
        {
            try
            {
                var apiService = IPlatformApplication.Current?.Services.GetService<ApiService>();
                if (apiService == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageService] No ApiService, skip sync");
                    return;
                }

                var serverTranslations = await apiService.GetUiTranslationsAsync();
                if (serverTranslations == null || serverTranslations.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageService] Server returned no translations, keeping local");
                    return;
                }

                // Ghi đè resources bằng dữ liệu từ server
                foreach (var (lang, dict) in serverTranslations)
                {
                    if (_resources.ContainsKey(lang))
                    {
                        // Merge: server values override local, giữ lại local keys không có trên server
                        foreach (var (key, val) in dict)
                        {
                            _resources[lang][key] = val;
                        }
                    }
                    else
                    {
                        _resources[lang] = dict;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[LanguageService] Synced {serverTranslations.Count} languages from server");
                
                // Trigger refresh UI nếu đang active
                OnLanguageChanged(CurrentLanguage);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] SyncFromServer error: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy danh sách ngôn ngữ đang active
        /// </summary>
        public static List<LanguageInfo> GetActiveLanguages() => SupportedLanguages.Where(l => l.IsActive).ToList();
        
        /// <summary>
        /// Debug: liệt kê tất cả loaded resources
        /// </summary>
        public static void DebugPrintResources()
        {
            System.Diagnostics.Debug.WriteLine("[LanguageService] === Loaded Resources ===");
            foreach (var lang in _resources)
            {
                System.Diagnostics.Debug.WriteLine($"  {lang.Key}: {lang.Value.Count} keys");
            }
        }
        
        /// <summary>
        /// Thông tin ngôn ngữ
        /// </summary>
        public class LanguageInfo
        {
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string NativeName { get; set; } = "";
            public bool IsActive { get; set; } = true;
        }
    }
}

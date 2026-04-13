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
        /// Mã ngôn ngữ hiện tại (vi, en, zh, ja, ko, fr, es, de, th, ru)
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
                ["PasswordTooShort"] = "Mật khẩu phải có ít nhất 6 ký tự", ["PasswordMismatch"] = "Mật khẩu xác nhận không khớp", ["ServerError"] = "Lỗi kết nối",
                ["SendCode"] = "Gửi mã", ["ResetPasswordButton"] = "Đặt lại mật khẩu", ["ResetCodeDemo"] = "Mã reset (demo):", ["DemoCode"] = "Mã demo",
                ["Login"] = "Đăng nhập", ["SignUp"] = "Đăng ký", ["ForgotPassword"] = "Quên mật khẩu?", ["Email"] = "Email", ["Password"] = "Mật khẩu",
                ["Home"] = "Trang chủ", ["Tours"] = "Tour", ["Map"] = "Bản đồ", ["Profile"] = "Hồ sơ",
                ["TabHome"] = "Trang chủ", ["TabFood"] = "Quán ăn", ["TabMap"] = "Bản đồ", ["TabTour"] = "Tour", ["TabProfile"] = "Tôi",
                ["Welcome"] = "Chào", ["SelectLanguage"] = "Chọn ngôn ngữ", ["Settings"] = "Cài đặt khóa ứng dụng", ["MyTours"] = "Các Tour Đã Đặt", ["ChangePassword"] = "Đổi mật khẩu", ["Logout"] = "Đăng xuất",
                ["SearchPlaceholder"] = "Tìm món, tìm quán, tìm tour...", ["CategoryNuong"] = "Nướng", ["CategoryLau"] = "Lẩu", ["CategoryOc"] = "Ốc", ["CategoryAnVat"] = "Ăn vặt",
                ["SuggestedTours"] = "🔥 Gợi ý Lộ trình", ["SeeMore"] = "Xem thêm >", ["TopPlaces"] = "Top Nổi Bật",
                ["SearchFood"] = "Tìm quán ăn...", ["DiscoverTours"] = "Khám Phá Tour", ["SearchTour"] = "Tìm tour...", ["StartTour"] = "Bắt đầu Tour",
                ["SearchMap"] = "Tìm quán ăn, điểm tham quan...", ["Search"] = "Tìm", ["ListenAudio"] = "🔊 Nghe", ["Directions"] = "🗺️ Đường đi", ["Close"] = "❌ Đóng",
                ["TourDetailTitle"] = "Chi tiết Tour", ["Introduction"] = "Giới thiệu", ["Destinations"] = "Các điểm đến", ["DownloadOfflineAudio"] = "⬇️ Tải Audio Offline", ["ViewMap"] = "Xem Bản Đồ", ["BookTour"] = "Đặt Tour"
            };
            
            var enDict = new Dictionary<string, string>
            {
                ["Error"] = "Error", ["Success"] = "Success", ["OK"] = "OK", ["Cancel"] = "Cancel", ["Loading"] = "Loading...",
                ["EmailRequired"] = "Please enter email", ["ResetCodeRequired"] = "Please enter reset code", ["NewPasswordRequired"] = "Please enter new password", 
                ["PasswordTooShort"] = "Password must be at least 6 characters", ["PasswordMismatch"] = "Passwords do not match", ["ServerError"] = "Connection Error",
                ["SendCode"] = "Send Code", ["ResetPasswordButton"] = "Reset Password", ["ResetCodeDemo"] = "Reset Code (demo):", ["DemoCode"] = "Demo Code",
                ["Login"] = "Login", ["SignUp"] = "Sign Up", ["ForgotPassword"] = "Forgot Password?", ["Email"] = "Email", ["Password"] = "Password",
                ["Home"] = "Home", ["Tours"] = "Tours", ["Map"] = "Map", ["Profile"] = "Profile",
                ["TabHome"] = "Home", ["TabFood"] = "Food", ["TabMap"] = "Map", ["TabTour"] = "Tour", ["TabProfile"] = "Me",
                ["Welcome"] = "Hello", ["SelectLanguage"] = "Select Language", ["Settings"] = "App Lock Setting", ["MyTours"] = "My Tours", ["ChangePassword"] = "Change Password", ["Logout"] = "Logout",
                ["SearchPlaceholder"] = "Search food, places, tours...", ["CategoryNuong"] = "BBQ", ["CategoryLau"] = "Hotpot", ["CategoryOc"] = "Snails", ["CategoryAnVat"] = "Snacks",
                ["SuggestedTours"] = "🔥 Suggested Tours", ["SeeMore"] = "See more >", ["TopPlaces"] = "Top Rated",
                ["SearchFood"] = "Search food...", ["DiscoverTours"] = "Discover Tours", ["SearchTour"] = "Search tours...", ["StartTour"] = "Start Tour",
                ["SearchMap"] = "Search places, tours...", ["Search"] = "Search", ["ListenAudio"] = "🔊 Listen", ["Directions"] = "🗺️ Directions", ["Close"] = "❌ Close",
                ["TourDetailTitle"] = "Tour Details", ["Introduction"] = "Introduction", ["Destinations"] = "Destinations", ["DownloadOfflineAudio"] = "⬇️ Download Offline Audio", ["ViewMap"] = "View Map", ["BookTour"] = "Book Tour"
            };

            var zhDict = new Dictionary<string, string>(enDict)
            {
                ["TabHome"] = "主页", ["TabFood"] = "食品", ["TabMap"] = "地图", ["TabTour"] = "游览", ["TabProfile"] = "我",
                ["Welcome"] = "你好", ["SelectLanguage"] = "选择语言", ["Logout"] = "登出"
            };

            var jaDict = new Dictionary<string, string>(enDict)
            {
                ["TabHome"] = "ホーム", ["TabFood"] = "食べ物", ["TabMap"] = "マップ", ["TabTour"] = "ツアー", ["TabProfile"] = "私",
                ["Welcome"] = "こんにちは", ["SelectLanguage"] = "言語を選択", ["Logout"] = "ログアウト"
            };

            var koDict = new Dictionary<string, string>(enDict)
            {
                ["TabHome"] = "홈", ["TabFood"] = "음식", ["TabMap"] = "지도", ["TabTour"] = "투어", ["TabProfile"] = "나",
                ["Welcome"] = "안녕하세요", ["SelectLanguage"] = "언어 선택", ["Logout"] = "로그아웃"
            };

            var frDict = new Dictionary<string, string>(enDict) { ["TabHome"] = "Accueil", ["TabFood"] = "Nourriture", ["TabMap"] = "Carte", ["TabTour"] = "Tour", ["TabProfile"] = "Moi", ["Welcome"] = "Bonjour", ["SelectLanguage"] = "Choisir", ["Logout"] = "Déconnexion" };
            var deDict = new Dictionary<string, string>(enDict) { ["TabHome"] = "Startseite", ["TabFood"] = "Essen", ["TabMap"] = "Karte", ["TabTour"] = "Tour", ["TabProfile"] = "Ich", ["Welcome"] = "Hallo", ["SelectLanguage"] = "Sprache", ["Logout"] = "Abmelden" };
            var esDict = new Dictionary<string, string>(enDict) { ["TabHome"] = "Inicio", ["TabFood"] = "Comida", ["TabMap"] = "Mapa", ["TabTour"] = "Tour", ["TabProfile"] = "Yo", ["Welcome"] = "Hola", ["SelectLanguage"] = "Idioma", ["Logout"] = "Cerrar sesión" };
            var thDict = new Dictionary<string, string>(enDict) { ["TabHome"] = "หน้าแรก", ["TabFood"] = "อาหาร", ["TabMap"] = "แผนที่", ["TabTour"] = "ทัวร์", ["TabProfile"] = "ฉัน", ["Welcome"] = "สวัสดี", ["SelectLanguage"] = "ภาษา", ["Logout"] = "ออกจากระบบ" };
            var ruDict = new Dictionary<string, string>(enDict) { ["TabHome"] = "Главная", ["TabFood"] = "Еда", ["TabMap"] = "Карта", ["TabTour"] = "Тур", ["TabProfile"] = "Я", ["Welcome"] = "Привет", ["SelectLanguage"] = "Язык", ["Logout"] = "Выйти" };

            _resources["vi"] = fallbackDict;
            _resources["en"] = enDict;
            _resources["zh"] = zhDict;
            _resources["ja"] = jaDict;
            _resources["ko"] = koDict;
            _resources["fr"] = frDict;
            _resources["de"] = deDict;
            _resources["es"] = esDict;
            _resources["th"] = thDict;
            _resources["ru"] = ruDict;
            
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Created built-in fallback for all 10 languages");
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
        /// Lấy string đã localize theo key
        /// </summary>
        public static string GetString(string key)
        {
            if (!_isInitialized)
                Initialize();
            
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
                "ko" => "한국어",
                "fr" => "Français",
                "de" => "Deutsch",
                "es" => "Español",
                "th" => "ไทย",
                "ru" => "Русский",
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
            new LanguageInfo { Code = "ja", Name = "Japanese", NativeName = "日本語", IsActive = true },
            new LanguageInfo { Code = "ko", Name = "Korean", NativeName = "한국어", IsActive = true },
            new LanguageInfo { Code = "fr", Name = "French", NativeName = "Français", IsActive = true },
            new LanguageInfo { Code = "de", Name = "German", NativeName = "Deutsch", IsActive = true },
            new LanguageInfo { Code = "es", Name = "Spanish", NativeName = "Español", IsActive = true },
            new LanguageInfo { Code = "th", Name = "Thai", NativeName = "ไทย", IsActive = true },
            new LanguageInfo { Code = "ru", Name = "Russian", NativeName = "Русский", IsActive = true }
        };
        
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

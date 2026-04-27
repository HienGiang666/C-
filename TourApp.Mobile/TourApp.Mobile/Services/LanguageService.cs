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
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Initialized with language: {savedLang}, resources: {_resources.Count} languages");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Init error: {ex.GetType().Name}: {ex.Message}");
                // Ensure we have fallback resources even if init fails
                if (_resources.Count == 0)
                {
                    CreateBuiltInFallback();
                }
                _isInitialized = true;
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
            
            // Log first few resource names for debugging
            foreach (var name in resourceNames.Take(10))
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Resource: {name}");
            }
            
            // Tìm tất cả Strings*.resx files (XML format only, skip .resources binary files)
            var resxCount = 0;
            foreach (var resourceName in resourceNames)
            {
                // Only process .resx files (XML), skip .resources (binary compiled)
                if (resourceName.Contains("Strings") && resourceName.EndsWith(".resx"))
                {
                    resxCount++;
                    var langCode = ExtractLanguageCode(resourceName);
                    LoadResourceFile(assembly, resourceName, langCode);
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[LanguageService] Processed {resxCount} .resx files, loaded {_resources.Count} languages");
            
            // Nếu không tìm thấy resource nào, load từ file system (fallback)
            if (_resources.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[LanguageService] No embedded .resx resources found, trying file system...");
                LoadResourcesFromFileSystem();
            }
        }
        
        /// <summary>
        /// Extract language code từ resource name (e.g., "TourApp.Mobile.Resources.Strings.en.resx" -> "en")
        /// </summary>
        private static string ExtractLanguageCode(string resourceName)
        {
            // Remove .resx extension first
            var nameWithoutExt = resourceName;
            if (resourceName.EndsWith(".resx"))
            {
                nameWithoutExt = resourceName.Substring(0, resourceName.Length - 5);
            }
            
            // Pattern: TourApp.Mobile.Resources.Strings.en hoặc TourApp.Mobile.Resources.Strings
            var parts = nameWithoutExt.Split('.');
            if (parts.Length >= 1)
            {
                var lastPart = parts[^1]; // Last part
                var secondLastPart = parts.Length >= 2 ? parts[^2] : null;
                
                // Case 1: Strings.en (secondLastPart is "Strings", lastPart is "en")
                if (secondLastPart == "Strings" && !string.IsNullOrEmpty(lastPart))
                {
                    return lastPart.ToLower();
                }
                
                // Case 2: Just "Strings" (default language)
                if (lastPart == "Strings")
                {
                    return DefaultLanguage;
                }
                
                // Case 3: Strings_en or other patterns
                if (lastPart.StartsWith("Strings") && lastPart.Length > 7)
                {
                    var langPart = lastPart.Substring(7).Trim('_', '.');
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
                if (stream == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Stream null for {resourceName}");
                    return;
                }
                
                if (stream.Length == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Stream empty for {resourceName}");
                    return;
                }
                
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
                
                if (dict.Count > 0)
                {
                    _resources[langCode] = dict;
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Loaded {dict.Count} keys for '{langCode}' from {resourceName}");
                }
            }
            catch (System.Xml.XmlException xmlEx)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] XML parse error in {resourceName}: {xmlEx.Message}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to load {resourceName}: {ex.GetType().Name}: {ex.Message}");
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
                ["Categories"] = "Danh mục",
                ["SuggestedTours"] = "🔥 Gợi ý Lộ trình", ["SeeMore"] = "Xem thêm >", ["TopPlaces"] = "Top Nổi Bật",
                ["SearchFood"] = "Tìm quán ăn...", ["DiscoverTours"] = "Khám Phá Tour", ["SearchTour"] = "Tìm tour...", ["StartTour"] = "Bắt đầu Tour",
                ["SearchMap"] = "Tìm quán ăn, điểm tham quan...", ["Search"] = "Tìm", ["ListenAudio"] = "🔊 Nghe", ["Directions"] = "🗺️ Đường đi", ["Close"] = "❌ Đóng",
                ["TourDetailTitle"] = "Chi tiết Tour", ["Introduction"] = "Giới thiệu", ["Destinations"] = "Các điểm đến", ["DownloadOfflineAudio"] = "⬇️ Tải Audio Offline", ["ViewTour"] = "Xem Tour", ["Route"] = "Lộ trình", ["BookTour"] = "Đặt Tour",
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
                ["QRInvalid"] = "Mã QR không hợp lệ. Vui lòng thử lại.",
                // ProfilePage keys
                ["Guest"] = "Khách", ["GuestMode"] = "Đang dùng chế độ khách", ["EditProfile"] = "Chỉnh sửa hồ sơ",
                ["ToursVisited"] = "Tour đã đi", ["PlacesTried"] = "Quán đã thử", ["Favorites"] = "Yêu thích",
                // BookingPage keys
                ["BookTourTitle"] = "Đặt Tour", ["SelectedTour"] = "Tour đang chọn", ["PricePerPerson"] = "Giá/Người:",
                ["BookingInfo"] = "Thông tin đặt chỗ", ["DepartureDate"] = "Ngày khởi hành", ["NumberOfPeople"] = "Số lượng người",
                ["NotesOptional"] = "Ghi chú (Tùy chọn)", ["SpecialRequests"] = "Yêu cầu đặc biệt...",
                ["TotalPayment"] = "Tổng thanh toán:", ["ConfirmBooking"] = "Xác nhận đặt tour",
                ["Processing"] = "Đang xử lý...", ["LoginRequired"] = "Yêu cầu đăng nhập",
                ["LoginToBook"] = "Bạn cần đăng nhập để đặt tour. Đăng nhập ngay?",
                ["BookingSuccess"] = "Cảm ơn bạn đã đặt tour. Vui lòng đợi quản trị viên xác nhận!",
                ["MaxParticipants"] = "Tour này tối đa {0} người.", ["Notice"] = "Thông báo",
                // EditProfilePage keys
                ["EditProfileTitle"] = "Chỉnh sửa hồ sơ", ["FullName"] = "Họ và tên", ["EnterFullName"] = "Nhập họ và tên",
                ["EnterEmail"] = "Nhập email", ["Phone"] = "Số điện thoại", ["EnterPhone"] = "Nhập số điện thoại",
                ["Address"] = "Địa chỉ", ["EnterAddress"] = "Nhập địa chỉ", ["DateOfBirth"] = "Ngày sinh",
                ["SaveChanges"] = "Lưu thay đổi", ["UserNotFound"] = "Không tìm thấy thông tin người dùng",
                ["FullNameRequired"] = "Vui lòng nhập họ và tên", ["UpdateSuccess"] = "Cập nhật thông tin thành công!",
                ["UpdateFailed"] = "Cập nhật thất bại: {0}", ["ConnectionError"] = "Lỗi kết nối: {0}",
                // TourDetailPage extra keys
                ["TourName"] = "Tên Tour", ["MaxPeople"] = "Tối đa {0}", ["PoiCount"] = "{0} điểm",
                ["Duration"] = "Thời lượng", ["PerPerson"] = "/ người", ["MaxPeopleLabel"] = "Tối đa",
                // Auth pages keys
                ["AppSlogan"] = "Khám phá ẩm thực quanh bạn", ["Username"] = "Tài khoản", ["RememberLogin"] = "Ghi nhớ đăng nhập",
                ["NoAccount"] = "Chưa có tài khoản? ", ["SignUpNow"] = "Đăng ký ngay", ["ContinueAsGuest"] = "Tiếp tục với tư cách khách",
                ["SignUpTitle"] = "Đăng ký", ["SignUpSubtitle"] = "Tạo tài khoản để bắt đầu khám phá", ["ConfirmPassword"] = "Nhập lại mật khẩu",
                ["ForgotPasswordTitle"] = "Quên mật khẩu?", ["ForgotPasswordSubtitle"] = "Nhập email để nhận mã xác nhận",
                ["EnterYourEmail"] = "Nhập email của bạn", ["SendVerificationCode"] = "GỬi mã xác nhận",
                ["ResetPasswordTitle"] = "Đặt lại mật khẩu", ["ResetPasswordSubtitle"] = "Nhập mã và mật khẩu mới",
                ["VerificationCode"] = "Mã xác nhận", ["EnterVerificationCode"] = "Nhập mã xác nhận",
                ["EnterNewPassword"] = "Nhập mật khẩu mới", ["ReEnterNewPassword"] = "Nhập lại mật khẩu mới",
                ["VerificationTitle"] = "Xác nhận", ["VerificationSubtitle"] = "Chúng tôi đã gửi mã đến email của bạn",
                ["ResendAfter"] = "Gửi lại sau {0}s", ["Verify"] = "Xác nhận",
                ["LoginRequiredUsername"] = "Vui lòng nhập tên đăng nhập",
                ["LoginRequiredPassword"] = "Vui lòng nhập mật khẩu",
                ["LoginFailed"] = "Đăng nhập thất bại",
                ["SignUpSuccess"] = "Đăng ký tài khoản thành công!",
                ["SignUpFailed"] = "Đăng ký thất bại",
                ["NoResetCode"] = "Không nhận được mã xác nhận",
                ["ConnectionErrorDetail"] = "Lỗi kết nối: {0}",
                // MapPage keys
                ["MockModeBanner"] = "📍 Chế độ giả lập — Chạm bản đồ để di chuyển",
                ["PoiNamePlaceholder"] = "Tên quán", ["Locating"] = "Đang xác định...", ["NowPlaying"] = "Đang phát...",
                ["SearchResultFromGoong"] = "Kết quả từ Goong Maps",
                ["TourRouteDisplay"] = "Đang hiển thị lộ trình tour: {0}",
                ["GPSError"] = "Chưa xác định được vị trí của bạn.",
                ["RouteNotFound"] = "Không tìm thấy đường đi.",
                ["DirectionsAPIError"] = "Không thể gọi API chỉ đường, vui lòng kiểm tra mạng.",
                ["NoDestinations"] = "Tour hiện tại chưa có điểm đến nào.",
                ["DownloadAudioSuccess"] = "Đã tải {0}/{1} file audio ngoại tuyến.",
                ["DownloadAudioFailed"] = "Tải audio thất bại.",
                ["CurrentPasswordRequired"] = "Vui lòng nhập mật khẩu hiện tại",
                ["CurrentPasswordWrong"] = "Mật khẩu hiện tại không đúng",
                ["ChangePasswordSuccess"] = "Đổi mật khẩu thành công!",
                ["ChangePasswordFailed"] = "Đổi mật khẩu thất bại: {0}",
                ["NoToursFound"] = "Không tìm thấy tour nào cho '{0}'",
                ["CameraPermissionRequired"] = "Cần quyền camera để quét QR"
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
                ["Categories"] = "Categories",
                ["SuggestedTours"] = "🔥 Suggested Tours", ["SeeMore"] = "See more >", ["TopPlaces"] = "Top Rated",
                ["SearchFood"] = "Search food...", ["DiscoverTours"] = "Discover Tours", ["SearchTour"] = "Search tours...", ["StartTour"] = "Start Tour",
                ["SearchMap"] = "Search places, tours...", ["Search"] = "Search", ["ListenAudio"] = "🔊 Listen", ["Directions"] = "🗺️ Directions", ["Close"] = "❌ Close",
                ["TourDetailTitle"] = "Tour Details", ["Introduction"] = "Introduction", ["Destinations"] = "Destinations", ["DownloadOfflineAudio"] = "⬇️ Download Offline Audio", ["ViewTour"] = "View Tour", ["Route"] = "Route", ["BookTour"] = "Book Tour",
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
                ["QRInvalid"] = "Invalid QR code. Please try again.",
                // ProfilePage keys
                ["Guest"] = "Guest", ["GuestMode"] = "Using guest mode", ["EditProfile"] = "Edit Profile",
                ["ToursVisited"] = "Tours", ["PlacesTried"] = "Places", ["Favorites"] = "Favorites",
                // BookingPage keys
                ["BookTourTitle"] = "Book Tour", ["SelectedTour"] = "Selected Tour", ["PricePerPerson"] = "Price/Person:",
                ["BookingInfo"] = "Booking Information", ["DepartureDate"] = "Departure Date", ["NumberOfPeople"] = "Number of People",
                ["NotesOptional"] = "Notes (Optional)", ["SpecialRequests"] = "Special requests...",
                ["TotalPayment"] = "Total Payment:", ["ConfirmBooking"] = "Confirm Booking",
                ["Processing"] = "Processing...", ["LoginRequired"] = "Login Required",
                ["LoginToBook"] = "Please login to book a tour. Login now?",
                ["BookingSuccess"] = "Thank you for booking! Please wait for admin confirmation.",
                ["MaxParticipants"] = "This tour allows max {0} people.", ["Notice"] = "Notice",
                // EditProfilePage keys
                ["EditProfileTitle"] = "Edit Profile", ["FullName"] = "Full Name", ["EnterFullName"] = "Enter full name",
                ["EnterEmail"] = "Enter email", ["Phone"] = "Phone Number", ["EnterPhone"] = "Enter phone number",
                ["Address"] = "Address", ["EnterAddress"] = "Enter address", ["DateOfBirth"] = "Date of Birth",
                ["SaveChanges"] = "Save Changes", ["UserNotFound"] = "User information not found",
                ["FullNameRequired"] = "Please enter your full name", ["UpdateSuccess"] = "Profile updated successfully!",
                ["UpdateFailed"] = "Update failed: {0}", ["ConnectionError"] = "Connection error: {0}",
                // TourDetailPage extra keys
                ["TourName"] = "Tour Name", ["MaxPeople"] = "Max {0}", ["PoiCount"] = "{0} stops",
                ["Duration"] = "Duration", ["PerPerson"] = "/ person", ["MaxPeopleLabel"] = "Max people",
                // Auth pages keys
                ["AppSlogan"] = "Discover food around you", ["Username"] = "Username", ["RememberLogin"] = "Remember me",
                ["NoAccount"] = "Don't have an account? ", ["SignUpNow"] = "Sign up now", ["ContinueAsGuest"] = "Continue as guest",
                ["SignUpTitle"] = "Sign Up", ["SignUpSubtitle"] = "Create an account to start exploring", ["ConfirmPassword"] = "Confirm password",
                ["ForgotPasswordTitle"] = "Forgot Password?", ["ForgotPasswordSubtitle"] = "Enter email to receive verification code",
                ["EnterYourEmail"] = "Enter your email", ["SendVerificationCode"] = "Send verification code",
                ["ResetPasswordTitle"] = "Reset Password", ["ResetPasswordSubtitle"] = "Enter code and new password",
                ["VerificationCode"] = "Verification code", ["EnterVerificationCode"] = "Enter verification code",
                ["EnterNewPassword"] = "Enter new password", ["ReEnterNewPassword"] = "Re-enter new password",
                ["VerificationTitle"] = "Verify", ["VerificationSubtitle"] = "We sent a code to your email",
                ["ResendAfter"] = "Resend in {0}s", ["Verify"] = "Verify",
                ["LoginRequiredUsername"] = "Please enter username",
                ["LoginRequiredPassword"] = "Please enter password",
                ["LoginFailed"] = "Login failed",
                ["SignUpSuccess"] = "Account created successfully!",
                ["SignUpFailed"] = "Sign up failed",
                ["NoResetCode"] = "No reset code received",
                ["ConnectionErrorDetail"] = "Connection error: {0}",
                // MapPage keys
                ["MockModeBanner"] = "📍 Mock mode — Tap map to move",
                ["PoiNamePlaceholder"] = "Place name", ["Locating"] = "Locating...", ["NowPlaying"] = "Now playing...",
                ["SearchResultFromGoong"] = "Result from Goong Maps",
                ["TourRouteDisplay"] = "Showing tour route: {0}",
                ["GPSError"] = "Cannot determine your location.",
                ["RouteNotFound"] = "Route not found.",
                ["DirectionsAPIError"] = "Cannot call directions API, please check your network.",
                ["NoDestinations"] = "This tour has no destinations yet.",
                ["DownloadAudioSuccess"] = "Downloaded {0}/{1} offline audio files.",
                ["DownloadAudioFailed"] = "Failed to download audio.",
                ["CurrentPasswordRequired"] = "Please enter current password",
                ["CurrentPasswordWrong"] = "Current password is incorrect",
                ["ChangePasswordSuccess"] = "Password changed successfully!",
                ["ChangePasswordFailed"] = "Failed to change password: {0}",
                ["NoToursFound"] = "No tours found for '{0}'",
                ["CameraPermissionRequired"] = "Camera permission is required to scan QR"
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
                ["Categories"] = "分类",
                ["SuggestedTours"] = "🔥 推荐路线", ["SeeMore"] = "查看更多 >", ["TopPlaces"] = "热门推荐",
                ["SearchFood"] = "搜索餐厅...", ["DiscoverTours"] = "探索旅游", ["SearchTour"] = "搜索旅游...", ["StartTour"] = "开始旅游",
                ["SearchMap"] = "搜索餐厅、景点...", ["Search"] = "搜索", ["ListenAudio"] = "🔊 收听", ["Directions"] = "🗺️ 导航", ["Close"] = "❌ 关闭",
                ["TourDetailTitle"] = "旅游详情", ["Introduction"] = "简介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ 下载离线音频", ["ViewTour"] = "查看旅游", ["Route"] = "路线", ["BookTour"] = "预订旅游",
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
                ["QRInvalid"] = "二维码无效。请重试。",
                // ProfilePage keys
                ["Guest"] = "游客", ["GuestMode"] = "游客模式", ["EditProfile"] = "编辑资料",
                ["ToursVisited"] = "已去", ["PlacesTried"] = "已尝试", ["Favorites"] = "收藏",
                // BookingPage keys
                ["BookTourTitle"] = "预订旅游", ["SelectedTour"] = "已选旅游", ["PricePerPerson"] = "每人价格：",
                ["BookingInfo"] = "预订信息", ["DepartureDate"] = "出发日期", ["NumberOfPeople"] = "人数",
                ["NotesOptional"] = "备注（可选）", ["SpecialRequests"] = "特殊要求...",
                ["TotalPayment"] = "总付款：", ["ConfirmBooking"] = "确认预订",
                ["Processing"] = "处理中...", ["LoginRequired"] = "需要登录",
                ["LoginToBook"] = "请登录以预订旅游。现在登录？",
                ["BookingSuccess"] = "感谢您的预订！请等待管理员确认。",
                ["MaxParticipants"] = "此旅游最多{0}人。", ["Notice"] = "通知",
                // EditProfilePage keys
                ["EditProfileTitle"] = "编辑资料", ["FullName"] = "姓名", ["EnterFullName"] = "请输入姓名",
                ["EnterEmail"] = "请输入邮箱", ["Phone"] = "电话号码", ["EnterPhone"] = "请输入电话号码",
                ["Address"] = "地址", ["EnterAddress"] = "请输入地址", ["DateOfBirth"] = "出生日期",
                ["SaveChanges"] = "保存更改", ["UserNotFound"] = "未找到用户信息",
                ["FullNameRequired"] = "请输入姓名", ["UpdateSuccess"] = "资料更新成功！",
                ["UpdateFailed"] = "更新失败：{0}", ["ConnectionError"] = "连接错误：{0}",
                // TourDetailPage extra keys
                ["TourName"] = "旅游名称", ["MaxPeople"] = "最多{0}人", ["PoiCount"] = "{0}个景点",
                ["Duration"] = "时长", ["PerPerson"] = "/ 每人", ["MaxPeopleLabel"] = "最多人数",
                // Auth pages keys
                ["AppSlogan"] = "发现你身边的美食", ["Username"] = "用户名", ["RememberLogin"] = "记住登录",
                ["NoAccount"] = "还没有账号？", ["SignUpNow"] = "立即注册", ["ContinueAsGuest"] = "以游客身份继续",
                ["SignUpTitle"] = "注册", ["SignUpSubtitle"] = "创建账号开始探索", ["ConfirmPassword"] = "确认密码",
                ["ForgotPasswordTitle"] = "忘记密码？", ["ForgotPasswordSubtitle"] = "输入邮箱以接收验证码",
                ["EnterYourEmail"] = "请输入邮箱", ["SendVerificationCode"] = "发送验证码",
                ["ResetPasswordTitle"] = "重置密码", ["ResetPasswordSubtitle"] = "输入验证码和新密码",
                ["VerificationCode"] = "验证码", ["EnterVerificationCode"] = "输入验证码",
                ["EnterNewPassword"] = "输入新密码", ["ReEnterNewPassword"] = "再次输入新密码",
                ["VerificationTitle"] = "验证", ["VerificationSubtitle"] = "我们已向您的邮箱发送验证码",
                ["ResendAfter"] = "{0}秒后重发", ["Verify"] = "验证",
                ["LoginRequiredUsername"] = "请输入用户名",
                ["LoginRequiredPassword"] = "请输入密码",
                ["LoginFailed"] = "登录失败",
                ["SignUpSuccess"] = "注册成功！",
                ["SignUpFailed"] = "注册失败",
                ["NoResetCode"] = "未收到验证码",
                ["ConnectionErrorDetail"] = "连接错误：{0}",
                // MapPage keys
                ["MockModeBanner"] = "📍 模拟模式 — 点击地图移动",
                ["PoiNamePlaceholder"] = "地点名称", ["Locating"] = "正在定位...", ["NowPlaying"] = "正在播放...",
                ["SearchResultFromGoong"] = "Goong Maps 搜索结果",
                ["TourRouteDisplay"] = "正在显示旅游路线：{0}",
                ["GPSError"] = "无法确定您的位置。",
                ["RouteNotFound"] = "未找到路线。",
                ["DirectionsAPIError"] = "无法调用导航API，请检查网络。",
                ["NoDestinations"] = "此旅游还没有任何目的地。",
                ["DownloadAudioSuccess"] = "已下载 {0}/{1} 个离线音频文件。",
                ["DownloadAudioFailed"] = "下载音频失败。",
                ["CurrentPasswordRequired"] = "请输入当前密码",
                ["CurrentPasswordWrong"] = "当前密码不正确",
                ["ChangePasswordSuccess"] = "密码修改成功！",
                ["ChangePasswordFailed"] = "密码修改失败：{0}",
                ["NoToursFound"] = "未找到'{0}'相关旅游",
                ["CameraPermissionRequired"] = "需要相机权限来扫描二维码"
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
                ["Categories"] = "カテゴリー",
                ["SuggestedTours"] = "🔥 おすすめルート", ["SeeMore"] = "もっと見る >", ["TopPlaces"] = "人気スポット",
                ["SearchFood"] = "レストラン検索...", ["DiscoverTours"] = "ツアーを探す", ["SearchTour"] = "ツアー検索...", ["StartTour"] = "ツアー開始",
                ["SearchMap"] = "レストラン、観光地を検索...", ["Search"] = "検索", ["ListenAudio"] = "🔊 再生", ["Directions"] = "🗺️ 経路", ["Close"] = "❌ 閉じる",
                ["TourDetailTitle"] = "ツアー詳細", ["Introduction"] = "紹介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ オフライン音声DL", ["ViewTour"] = "ツアーを見る", ["Route"] = "ルート", ["BookTour"] = "ツアー予約",
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
                ["QRInvalid"] = "無効なQRコードです。もう一度お試しください。",
                // ProfilePage keys
                ["Guest"] = "ゲスト", ["GuestMode"] = "ゲストモード", ["EditProfile"] = "プロフィール編集",
                ["ToursVisited"] = "ツアー", ["PlacesTried"] = "訪問先", ["Favorites"] = "お気に入り",
                // BookingPage keys
                ["BookTourTitle"] = "ツアー予約", ["SelectedTour"] = "選択中のツアー", ["PricePerPerson"] = "1人あたり：",
                ["BookingInfo"] = "予約情報", ["DepartureDate"] = "出発日", ["NumberOfPeople"] = "人数",
                ["NotesOptional"] = "メモ（任意）", ["SpecialRequests"] = "特別なリクエスト...",
                ["TotalPayment"] = "合計金額：", ["ConfirmBooking"] = "予約を確定",
                ["Processing"] = "処理中...", ["LoginRequired"] = "ログインが必要",
                ["LoginToBook"] = "ツアーを予約するにはログインが必要です。今すぐログイン？",
                ["BookingSuccess"] = "ご予約ありがとうございます！管理者の確認をお待ちください。",
                ["MaxParticipants"] = "このツアーは最大{0}人です。", ["Notice"] = "お知らせ",
                // EditProfilePage keys
                ["EditProfileTitle"] = "プロフィール編集", ["FullName"] = "氏名", ["EnterFullName"] = "氏名を入力",
                ["EnterEmail"] = "メールを入力", ["Phone"] = "電話番号", ["EnterPhone"] = "電話番号を入力",
                ["Address"] = "住所", ["EnterAddress"] = "住所を入力", ["DateOfBirth"] = "生年月日",
                ["SaveChanges"] = "変更を保存", ["UserNotFound"] = "ユーザー情報が見つかりません",
                ["FullNameRequired"] = "氏名を入力してください", ["UpdateSuccess"] = "プロフィールを更新しました！",
                ["UpdateFailed"] = "更新失敗：{0}", ["ConnectionError"] = "接続エラー：{0}",
                // TourDetailPage extra keys
                ["TourName"] = "ツアー名", ["MaxPeople"] = "最大{0}人", ["PoiCount"] = "{0}箇所",
                ["Duration"] = "所要時間", ["PerPerson"] = "/ 1人", ["MaxPeopleLabel"] = "最大人数",
                // Auth pages keys
                ["AppSlogan"] = "周りのグルメを探索", ["Username"] = "ユーザー名", ["RememberLogin"] = "ログインを記憶",
                ["NoAccount"] = "アカウントがない？", ["SignUpNow"] = "今すぐ登録", ["ContinueAsGuest"] = "ゲストとして続行",
                ["SignUpTitle"] = "サインアップ", ["SignUpSubtitle"] = "アカウントを作成して探索を始めましょう", ["ConfirmPassword"] = "パスワード確認",
                ["ForgotPasswordTitle"] = "パスワードを忘れた？", ["ForgotPasswordSubtitle"] = "メールを入力して認証コードを受け取る",
                ["EnterYourEmail"] = "メールを入力", ["SendVerificationCode"] = "認証コードを送信",
                ["ResetPasswordTitle"] = "パスワードリセット", ["ResetPasswordSubtitle"] = "コードと新しいパスワードを入力",
                ["VerificationCode"] = "認証コード", ["EnterVerificationCode"] = "認証コードを入力",
                ["EnterNewPassword"] = "新しいパスワードを入力", ["ReEnterNewPassword"] = "新しいパスワードを再入力",
                ["VerificationTitle"] = "認証", ["VerificationSubtitle"] = "メールにコードを送信しました",
                ["ResendAfter"] = "{0}秒後に再送信", ["Verify"] = "認証",
                ["LoginRequiredUsername"] = "ユーザー名を入力してください",
                ["LoginRequiredPassword"] = "パスワードを入力してください",
                ["LoginFailed"] = "ログイン失敗",
                ["SignUpSuccess"] = "アカウント登録成功！",
                ["SignUpFailed"] = "登録失敗",
                ["NoResetCode"] = "認証コードを受信できませんでした",
                ["ConnectionErrorDetail"] = "接続エラー：{0}",
                // MapPage keys
                ["MockModeBanner"] = "📍 モックモード — マップをタップして移動",
                ["PoiNamePlaceholder"] = "場所名", ["Locating"] = "位置を特定中...", ["NowPlaying"] = "再生中...",
                ["SearchResultFromGoong"] = "Goong Maps の検索結果",
                ["TourRouteDisplay"] = "ツアールートを表示中：{0}",
                ["GPSError"] = "位置を特定できません。",
                ["RouteNotFound"] = "ルートが見つかりません。",
                ["DirectionsAPIError"] = "経路APIを呼び出せません。ネットワークを確認してください。",
                ["NoDestinations"] = "このツアーにはまだ目的地がありません。",
                ["DownloadAudioSuccess"] = "{0}/{1} 件のオフライン音声をダウンロードしました。",
                ["DownloadAudioFailed"] = "音声のダウンロードに失敗しました。",
                ["CurrentPasswordRequired"] = "現在のパスワードを入力してください",
                ["CurrentPasswordWrong"] = "現在のパスワードが間違っています",
                ["ChangePasswordSuccess"] = "パスワードを変更しました！",
                ["ChangePasswordFailed"] = "パスワード変更失敗：{0}",
                ["NoToursFound"] = "'{0}' のツアーが見つかりません",
                ["CameraPermissionRequired"] = "QRスキャンにはカメラ権限が必要です"
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
                // Validate langCode before creating CultureInfo
                if (string.IsNullOrWhiteSpace(langCode))
                {
                    langCode = DefaultLanguage;
                }
                
                CultureInfo culture;
                try
                {
                    culture = new CultureInfo(langCode);
                }
                catch (CultureNotFoundException)
                {
                    // Fallback to default if culture not found
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Culture '{langCode}' not found, falling back to '{DefaultLanguage}'");
                    culture = new CultureInfo(DefaultLanguage);
                }
                
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;
                CultureInfo.DefaultThreadCurrentCulture = culture;
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Culture set to: {culture.Name}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to set culture: {ex.GetType().Name}: {ex.Message}");
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
                // Defer sync if platform application not ready (e.g., during startup)
                var app = IPlatformApplication.Current;
                if (app == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageService] IPlatformApplication.Current is null, deferring GeofenceService sync");
                    // Defer sync to later when app is ready
                    Task.Run(async () =>
                    {
                        await Task.Delay(2000); // Wait 2 seconds for app to initialize
                        MainThread.BeginInvokeOnMainThread(() => SyncWithGeofenceService(lang));
                    });
                    return;
                }
                
                var services = app.Services;
                if (services == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageService] Services is null, skipping GeofenceService sync");
                    return;
                }
                
                var geofence = services.GetService<GeofenceService>();
                if (geofence != null)
                {
                    geofence.CurrentLanguage = lang;
                    System.Diagnostics.Debug.WriteLine($"[LanguageService] Synced GeofenceService language to: {lang}");
                }
            }
            catch (InvalidOperationException)
            {
                // Service provider not built yet, ignore
                System.Diagnostics.Debug.WriteLine("[LanguageService] Service provider not ready, skipping GeofenceService sync");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LanguageService] Failed to sync GeofenceService: {ex.GetType().Name}: {ex.Message}");
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

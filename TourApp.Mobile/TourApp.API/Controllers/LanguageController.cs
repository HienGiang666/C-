using Microsoft.AspNetCore.Mvc;

namespace TourApp.API.Controllers;

/// <summary>
/// Cung cấp danh sách ngôn ngữ hỗ trợ và bản dịch UI cho mobile app.
/// Mobile gọi endpoint này khi khởi động để đồng bộ text giao diện từ server.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class LanguageController : ControllerBase
{
    /// <summary>
    /// Danh sách ngôn ngữ mặc định (khớp CMS LanguageSettingsService.DefaultLanguages)
    /// </summary>
    private static readonly List<LanguageDto> SupportedLanguages = new()
    {
        new() { Code = "vi", Name = "Tiếng Việt", Locale = "vi-VN" },
        new() { Code = "en", Name = "English", Locale = "en-US" },
        new() { Code = "zh", Name = "中文", Locale = "zh-CN" },
        new() { Code = "ja", Name = "日本語", Locale = "ja-JP" }
    };

    /// <summary>
    /// UI translations cho tất cả ngôn ngữ — mobile sẽ tải bộ này khi khởi động
    /// </summary>
    private static readonly Dictionary<string, Dictionary<string, string>> UiTranslations = new()
    {
        ["vi"] = new()
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
        },
        ["en"] = new()
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
        },
        ["zh"] = new()
        {
            ["Error"] = "错误", ["Success"] = "成功", ["OK"] = "确定", ["Cancel"] = "取消", ["Loading"] = "加载中...",
            ["EmailRequired"] = "请输入邮箱", ["ResetCodeRequired"] = "请输入重置码", ["NewPasswordRequired"] = "请输入新密码",
            ["PasswordTooShort"] = "密码至少6个字符", ["PasswordMismatch"] = "确认密码不匹配", ["ServerError"] = "连接错误",
            ["SendCode"] = "发送验证码", ["ResetPasswordButton"] = "重置密码", ["ResetCodeDemo"] = "重置码(演示):", ["DemoCode"] = "演示码",
            ["Login"] = "登录", ["SignUp"] = "注册", ["ForgotPassword"] = "忘记密码?", ["Email"] = "邮箱", ["Password"] = "密码",
            ["Home"] = "首页", ["Tours"] = "旅游", ["Map"] = "地图", ["Profile"] = "个人",
            ["TabHome"] = "首页", ["TabFood"] = "美食", ["TabMap"] = "地图", ["TabTour"] = "旅游", ["TabProfile"] = "我的",
            ["Welcome"] = "你好", ["SelectLanguage"] = "选择语言", ["Settings"] = "应用锁定设置", ["MyTours"] = "我的旅游", ["ChangePassword"] = "修改密码", ["Logout"] = "退出登录",
            ["SearchPlaceholder"] = "搜索美食、餐厅、旅游...", ["CategoryNuong"] = "烧烤", ["CategoryLau"] = "火锅", ["CategoryOc"] = "海螺", ["CategoryAnVat"] = "小吃",
            ["SuggestedTours"] = "🔥 推荐路线", ["SeeMore"] = "查看更多 >", ["TopPlaces"] = "热门推荐",
            ["SearchFood"] = "搜索餐厅...", ["DiscoverTours"] = "探索旅游", ["SearchTour"] = "搜索旅游...", ["StartTour"] = "开始旅游",
            ["SearchMap"] = "搜索餐厅、景点...", ["Search"] = "搜索", ["ListenAudio"] = "🔊 收听", ["Directions"] = "🗺️ 导航", ["Close"] = "❌ 关闭",
            ["TourDetailTitle"] = "旅游详情", ["Introduction"] = "简介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ 下载离线音频", ["ViewMap"] = "查看地图", ["BookTour"] = "预订旅游"
        },
        ["ja"] = new()
        {
            ["Error"] = "エラー", ["Success"] = "成功", ["OK"] = "OK", ["Cancel"] = "キャンセル", ["Loading"] = "読み込み中...",
            ["EmailRequired"] = "メールアドレスを入力してください", ["ResetCodeRequired"] = "リセットコードを入力してください", ["NewPasswordRequired"] = "新しいパスワードを入力してください",
            ["PasswordTooShort"] = "パスワードは6文字以上", ["PasswordMismatch"] = "確認パスワードが一致しません", ["ServerError"] = "接続エラー",
            ["SendCode"] = "コード送信", ["ResetPasswordButton"] = "パスワードリセット", ["ResetCodeDemo"] = "リセットコード(デモ):", ["DemoCode"] = "デモコード",
            ["Login"] = "ログイン", ["SignUp"] = "サインアップ", ["ForgotPassword"] = "パスワードを忘れた?", ["Email"] = "メール", ["Password"] = "パスワード",
            ["Home"] = "ホーム", ["Tours"] = "ツアー", ["Map"] = "マップ", ["Profile"] = "プロフィール",
            ["TabHome"] = "ホーム", ["TabFood"] = "グルメ", ["TabMap"] = "マップ", ["TabTour"] = "ツアー", ["TabProfile"] = "マイ",
            ["Welcome"] = "こんにちは", ["SelectLanguage"] = "言語を選択", ["Settings"] = "アプリロック設定", ["MyTours"] = "マイツアー", ["ChangePassword"] = "パスワード変更", ["Logout"] = "ログアウト",
            ["SearchPlaceholder"] = "料理、レストラン、ツアーを検索...", ["CategoryNuong"] = "焼肉", ["CategoryLau"] = "鍋", ["CategoryOc"] = "貝", ["CategoryAnVat"] = "軽食",
            ["SuggestedTours"] = "🔥 おすすめルート", ["SeeMore"] = "もっと見る >", ["TopPlaces"] = "人気スポット",
            ["SearchFood"] = "レストラン検索...", ["DiscoverTours"] = "ツアーを探す", ["SearchTour"] = "ツアー検索...", ["StartTour"] = "ツアー開始",
            ["SearchMap"] = "レストラン、観光地を検索...", ["Search"] = "検索", ["ListenAudio"] = "🔊 再生", ["Directions"] = "🗺️ 経路", ["Close"] = "❌ 閉じる",
            ["TourDetailTitle"] = "ツアー詳細", ["Introduction"] = "紹介", ["Destinations"] = "目的地", ["DownloadOfflineAudio"] = "⬇️ オフライン音声DL", ["ViewMap"] = "地図を見る", ["BookTour"] = "ツアー予約"
        }
    };

    /// <summary>
    /// GET /api/Language — trả về danh sách ngôn ngữ hỗ trợ
    /// </summary>
    [HttpGet]
    public ActionResult<List<LanguageDto>> GetLanguages()
    {
        return SupportedLanguages;
    }

    /// <summary>
    /// GET /api/Language/translations — trả về toàn bộ bản dịch UI cho mobile
    /// </summary>
    [HttpGet("translations")]
    public ActionResult<Dictionary<string, Dictionary<string, string>>> GetTranslations()
    {
        return UiTranslations;
    }

    /// <summary>
    /// GET /api/Language/translations/{lang} — trả về bản dịch UI cho 1 ngôn ngữ
    /// </summary>
    [HttpGet("translations/{lang}")]
    public ActionResult<Dictionary<string, string>> GetTranslation(string lang)
    {
        if (UiTranslations.TryGetValue(lang.ToLower(), out var dict))
            return dict;
        return NotFound();
    }
}

public class LanguageDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Locale { get; set; } = "";
}

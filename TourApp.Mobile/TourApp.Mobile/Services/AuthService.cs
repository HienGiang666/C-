using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class AuthService
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

        public static User? CurrentUser { get; private set; }
        public static bool IsLoggedIn => CurrentUser != null;

        /// <summary>
        /// Hash password using SHA256 (same as API)
        /// </summary>
        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(password)));
        }

        /// <summary>
        /// Login user
        /// </summary>
        public async Task<(bool Success, string Message, User? User, bool IsNetworkError)> LoginAsync(string username, string password)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var request = new
                {
                    Username = username,
                    Password = password,
                    IsCms = false
                };

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsJsonAsync("/api/user/login", request);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Không thể kết nối đến máy chủ. Vui lòng kiểm tra WiFi hoặc địa chỉ API.", null, true);
                }
                catch (HttpRequestException ex)
                {
                    return (false, $"Lỗi kết nối: {ex.Message}", null, true);
                }
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(content, _jsonOptions);
                    
                    // Parse token from response
                    string? token = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(content);
                        if (doc.RootElement.TryGetProperty("token", out var tokenProp))
                            token = tokenProp.GetString();
                    }
                    catch { }
                    
                    if (user != null)
                    {
                        // Kiểm tra vai trò: Mobile chỉ dành cho Customer
                        if (user.Role != null && user.Role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, "Tài khoản Admin chỉ dùng cho CMS quản trị!", null, false);
                        }
                        if (user.Role != null && user.Role.Equals("RestaurantOwner", StringComparison.OrdinalIgnoreCase))
                        {
                            return (false, "Tài khoản Chủ quán chỉ dùng cho CMS quản trị!", null, false);
                        }

                        CurrentUser = user;
                        // Save to preferences for auto-login
                        Preferences.Default.Set("user_id", user.Id);
                        Preferences.Default.Set("user_username", user.Username);
                        Preferences.Default.Set("user_fullname", user.FullName ?? user.Username);
                        Preferences.Default.Set("user_email", user.Email ?? "");
                        Preferences.Default.Set("user_role", user.Role ?? "Customer");
                        Preferences.Default.Set("is_logged_in", true);
                        
                        // Save JWT token
                        if (!string.IsNullOrEmpty(token))
                            Preferences.Default.Set("auth_token", token);
                        
                        return (true, "Đăng nhập thành công!", user, false);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Sai tên đăng nhập hoặc mật khẩu!", null, false);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"Lỗi máy chủ ({(int)response.StatusCode}): {errorContent}", null, true);
                }

                return (false, "Lỗi không xác định.", null, true);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Login error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", null, true);
            }
        }

        /// <summary>
        /// Register new user
        /// </summary>
        public async Task<(bool Success, string Message, User? User)> RegisterAsync(string fullName, string username, string email, string password)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.Timeout = TimeSpan.FromSeconds(15);

                var request = new
                {
                    FullName = fullName,
                    Username = username,
                    Email = email,
                    PasswordHash = HashPassword(password),
                    Role = "Customer",
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsJsonAsync("/api/user", request);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Không thể kết nối đến máy chủ. Vui lòng kiểm tra WiFi hoặc địa chỉ API.", null);
                }
                catch (HttpRequestException ex)
                {
                    return (false, $"Lỗi kết nối: {ex.Message}", null);
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(content, _jsonOptions);
                    
                    if (user != null)
                    {
                        // Auto login after register
                        CurrentUser = user;
                        Preferences.Default.Set("user_id", user.Id);
                        Preferences.Default.Set("user_username", user.Username);
                        Preferences.Default.Set("user_fullname", user.FullName ?? user.Username);
                        Preferences.Default.Set("user_email", user.Email ?? "");
                        Preferences.Default.Set("user_role", user.Role ?? "Customer");
                        Preferences.Default.Set("is_logged_in", true);
                        
                        return (true, "Đăng ký thành công!", user);
                    }
                }

                // Xử lý lỗi từ API
                var errorContent = await response.Content.ReadAsStringAsync();
                
                // Parse message từ JSON response nếu có
                try
                {
                    var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                    if (errorObj.TryGetProperty("message", out var msgProp))
                        return (false, msgProp.GetString() ?? "Đăng ký thất bại!", null);
                }
                catch { }

                // 409 Conflict = trùng email/username
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    return (false, "Email hoặc tên đăng nhập đã tồn tại!", null);

                return (false, $"Đăng ký thất bại ({(int)response.StatusCode})", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Register error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Forgot password - Demo version (no email sent, code shown in UI)
        /// </summary>
        public async Task<(bool Success, string Message, string? DemoCode, bool IsNetworkError)> ForgotPasswordAsync(string email)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var request = new { Email = email };

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsJsonAsync("/api/user/forgot-password", request);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Không thể kết nối đến máy chủ. Vui lòng kiểm tra WiFi hoặc địa chỉ API.", null, true);
                }
                catch (HttpRequestException ex)
                {
                    return (false, $"Lỗi kết nối: {ex.Message}", null, true);
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    // Try to parse demo code from response
                    try
                    {
                        var result = JsonSerializer.Deserialize<ForgotPasswordResponse>(content, _jsonOptions);
                        return (true, result?.Message ?? "Mã reset đã được tạo", result?.DemoCode, false);
                    }
                    catch
                    {
                        // If API returns plain text or different format
                        return (true, "Mã reset đã được tạo", "123456", false); // Fallback demo code
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    return (false, "Email không tồn tại trong hệ thống.", null, false);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"Lỗi máy chủ ({(int)response.StatusCode}): {errorContent}", null, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Forgot password error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", null, true);
            }
        }

        /// <summary>
        /// Reset password with code
        /// </summary>
        public async Task<(bool Success, string Message, bool IsNetworkError)> ResetPasswordAsync(string email, string code, string newPassword)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;

                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var request = new
                {
                    Email = email,
                    Code = code,
                    NewPassword = HashPassword(newPassword)
                };

                HttpResponseMessage response;
                try
                {
                    response = await httpClient.PostAsJsonAsync("/api/user/reset-password", request);
                }
                catch (TaskCanceledException)
                {
                    return (false, "Không thể kết nối đến máy chủ. Vui lòng kiểm tra WiFi hoặc địa chỉ API.", true);
                }
                catch (HttpRequestException ex)
                {
                    return (false, $"Lỗi kết nối: {ex.Message}", true);
                }

                if (response.IsSuccessStatusCode)
                {
                    return (true, "Đổi mật khẩu thành công! Vui lòng đăng nhập lại.", false);
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"Mã reset không hợp lệ: {errorContent}", false);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    return (false, $"Lỗi máy chủ ({(int)response.StatusCode}): {errorContent}", true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Reset password error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", true);
            }
        }

        /// <summary>
        /// Response model for forgot password
        /// </summary>
        private class ForgotPasswordResponse
        {
            [JsonPropertyName("message")]
            public string? Message { get; set; }
            [JsonPropertyName("demoCode")]
            public string? DemoCode { get; set; }
        }

        /// <summary>
        /// Check if user has saved login session (including guest)
        /// </summary>
        public static bool HasSavedSession()
        {
            var isLoggedIn = Preferences.Default.Get("is_logged_in", false);
            var userId = Preferences.Default.Get("user_id", 0);
            var isGuestMode = Preferences.Default.Get("is_guest_mode", false);

            // Valid session: logged in as regular user (userId > 0) OR guest mode
            return isLoggedIn && (userId > 0 || isGuestMode);
        }

        /// <summary>
        /// Load saved user session (support both regular user and guest)
        /// </summary>
        public static void LoadSavedSession()
        {
            var isGuestMode = Preferences.Default.Get("is_guest_mode", false);

            if (isGuestMode)
            {
                // Tạo lại guest user từ session
                CurrentUser = CreateGuestUser();
                return;
            }

            if (HasSavedSession())
            {
                CurrentUser = new User
                {
                    Id = Preferences.Default.Get("user_id", 0),
                    Username = Preferences.Default.Get("user_username", ""),
                    FullName = Preferences.Default.Get("user_fullname", ""),
                    Email = Preferences.Default.Get("user_email", ""),
                    Role = Preferences.Default.Get("user_role", "Customer")
                };
            }
        }

        /// <summary>
        /// Logout user
        /// </summary>
        public static void Logout()
        {
            CurrentUser = null;
            Preferences.Default.Remove("user_id");
            Preferences.Default.Remove("user_username");
            Preferences.Default.Remove("user_fullname");
            Preferences.Default.Remove("user_email");
            Preferences.Default.Remove("user_role");
            Preferences.Default.Remove("auth_token");
            Preferences.Default.Set("is_logged_in", false);
            ClearGuestMode();
        }

        /// <summary>
        /// Get user bookings (tour history)
        /// </summary>
        public async Task<List<Booking>?> GetUserBookingsAsync(int userId)
        {
            try
            {
                var baseUrl = ApiService.BaseUrl;
                using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

                var response = await httpClient.GetAsync($"/api/user/{userId}/bookings");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return JsonSerializer.Deserialize<List<Booking>>(content, _jsonOptions);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Get bookings error: {ex.Message}");
            }
            return new List<Booking>();
        }

        /// <summary>
        /// Tạo user khách (guest) để đăng nhập không cần tài khoản
        /// </summary>
        public static User CreateGuestUser()
        {
            var guestId = Guid.NewGuid().ToString("N")[..8];
            return new User
            {
                Id = 0,
                Username = $"guest_{guestId}",
                FullName = "Khách",
                Email = null,
                Role = "Guest",
                IsActive = true,
                CreatedAt = DateTime.Now
            };
        }

        /// <summary>
        /// Đặt user hiện tại (dùng cho guest login)
        /// </summary>
        public static void SetCurrentUser(User user)
        {
            CurrentUser = user;

            if (user != null && user.Role != "Guest")
            {
                // Lưu preferences cho user thường (không lưu guest)
                Preferences.Default.Set("user_id", user.Id);
                Preferences.Default.Set("user_username", user.Username);
                Preferences.Default.Set("user_fullname", user.FullName ?? user.Username);
                Preferences.Default.Set("user_email", user.Email ?? "");
                Preferences.Default.Set("user_role", user.Role ?? "Customer");
                Preferences.Default.Set("is_logged_in", true);
            }
            else if (user?.Role == "Guest")
            {
                // Đánh dấu là đang dùng guest mode
                Preferences.Default.Set("is_guest_mode", true);
                Preferences.Default.Set("is_logged_in", true);
            }
        }

        /// <summary>
        /// Kiểm tra có đang ở guest mode không
        /// </summary>
        public static bool IsGuestMode => CurrentUser?.Role == "Guest" || Preferences.Default.Get("is_guest_mode", false);

        /// <summary>
        /// Xóa trạng thái guest mode khi logout
        /// </summary>
        public static void ClearGuestMode()
        {
            Preferences.Default.Remove("is_guest_mode");
        }
    }
}

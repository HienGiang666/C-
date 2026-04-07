using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class AuthService
    {
        private static readonly HttpClient _httpClient = new HttpClient();
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
        public async Task<(bool Success, string Message, User? User)> LoginAsync(string username, string password)
        {
            try
            {
                await ApiService.AutoDiscoverApiAsync();
                var baseUrl = ApiService.BaseUrl;
                _httpClient.BaseAddress = new Uri(baseUrl);

                var request = new
                {
                    Username = username,
                    Password = HashPassword(password)
                };

                var response = await _httpClient.PostAsJsonAsync("/api/user/login", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<User>(content, _jsonOptions);
                    
                    if (user != null)
                    {
                        CurrentUser = user;
                        // Save to preferences for auto-login
                        Preferences.Default.Set("user_id", user.Id);
                        Preferences.Default.Set("user_username", user.Username);
                        Preferences.Default.Set("user_fullname", user.FullName ?? user.Username);
                        Preferences.Default.Set("user_email", user.Email ?? "");
                        Preferences.Default.Set("user_role", user.Role ?? "Customer");
                        Preferences.Default.Set("is_logged_in", true);
                        
                        return (true, "Đăng nhập thành công!", user);
                    }
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    return (false, "Sai tên đăng nhập hoặc mật khẩu!", null);
                }

                return (false, "Không thể kết nối đến máy chủ.", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Login error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", null);
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
                _httpClient.BaseAddress = new Uri(baseUrl);

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

                var response = await _httpClient.PostAsJsonAsync("/api/user", request);
                
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

                var errorContent = await response.Content.ReadAsStringAsync();
                return (false, $"Đăng ký thất bại: {errorContent}", null);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AuthService] Register error: {ex.Message}");
                return (false, $"Lỗi: {ex.Message}", null);
            }
        }

        /// <summary>
        /// Check if user has saved login session
        /// </summary>
        public static bool HasSavedSession()
        {
            var isLoggedIn = Preferences.Default.Get("is_logged_in", false);
            var userId = Preferences.Default.Get("user_id", 0);
            // Only consider valid session if is_logged_in is true AND user_id is valid (> 0)
            return isLoggedIn && userId > 0;
        }

        /// <summary>
        /// Load saved user session
        /// </summary>
        public static void LoadSavedSession()
        {
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
            Preferences.Default.Set("is_logged_in", false);
        }

        /// <summary>
        /// Get user bookings (tour history)
        /// </summary>
        public async Task<List<Booking>?> GetUserBookingsAsync(int userId)
        {
            try
            {
                var baseUrl = ApiService.BaseUrl;
                _httpClient.BaseAddress = new Uri(baseUrl);
                
                var response = await _httpClient.GetAsync($"/api/user/{userId}/bookings");
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
    }
}

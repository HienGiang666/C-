using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ApiService()
        {
            // Cấu hình DevHttpsConnectionHelper để bypass SSL trên Android giả lập/thiết bị
#if ANDROID
            var handler = new HttpsClientHandlerService().GetPlatformMessageHandler();
            _httpClient = new HttpClient(handler);
#else
            _httpClient = new HttpClient();
#endif
            
            // TODO: BẠN CẦN THAY {IP} BẰNG ĐỊA CHỈ IP LAN CỦA MÁY TÍNH CỦA BẠN (VD: 192.168.1.5)
            // IP máy tính của bạn khi kết nối cùng wifi với điện thoại (chạy lệnh ipconfig trên CMD để xem ipv4)
            _baseUrl = "http://192.168.1.7:5254";
            
            _httpClient.BaseAddress = new Uri(_baseUrl);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/poi");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error testing connection: {ex.Message}");
                return false;
            }
        }

        public async Task<List<POI>> GetAllPOIsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/poi");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    var list = JsonSerializer.Deserialize<List<POI>>(content, options);
                    
                    // Map lại Id và Name vì Mobile dùng PoiId, PoiName
                    // Tốt nhất là dùng [JsonPropertyName("id")] trong Model POI,
                    // Nếu model chưa có thì gán tay tạm tại đây để tránh lỗi bind UI
                    // (Chúng ta sẽ update POI.cs sớm thôi)
                    
                    return list ?? new List<POI>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting POIs: {ex.Message}");
            }
            
            // Fallback trả về list trống nếu lỗi kết nối
            return new List<POI>();
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            await Task.CompletedTask;
            return null; // chưa có API audio thật, trả về null để xài TTS theo như cũ
        }

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            await Task.CompletedTask;
            // TODO: gọi HttpPost API log sau
        }
    }
    
    // Helper bypass SSL (quan trọng khi gọi API localhost từ App thật / Emulator)
    public class HttpsClientHandlerService
    {
        public HttpMessageHandler GetPlatformMessageHandler()
        {
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
            {
                if (cert != null && cert.Issuer.Equals("CN=localhost"))
                    return true;
                return errors == System.Net.Security.SslPolicyErrors.None;
            };
            return handler;
#else
            return new HttpClientHandler();
#endif
        }
    }
}

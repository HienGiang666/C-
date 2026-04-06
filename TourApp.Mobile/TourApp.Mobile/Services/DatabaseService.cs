using System.Net.Http.Json;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class DatabaseService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiBaseUrl = "https://localhost:7244/api"; // Change to your API URL

        public DatabaseService()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
            };
            _httpClient = new HttpClient(handler);
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/poi");
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi chi tiết", ex.Message, "OK");
                return false;
            }
        }

        public async Task<List<POI>> GetAllPOIsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/poi");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<List<POI>>() ?? new List<POI>();
                }
                return new List<POI>();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi", ex.Message, "OK");
                return new List<POI>();
            }
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_apiBaseUrl}/audio/poi/{poiId}?lang={lang}");
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Audio>();
                }
                return null;
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi", ex.Message, "OK");
                return null;
            }
        }

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            try
            {
                var log = new { poiId, audioId, triggerType, deviceId = DeviceInfo.Current.Name };
                var response = await _httpClient.PostAsJsonAsync($"{_apiBaseUrl}/narration-log", log);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                await Shell.Current.DisplayAlert("Lỗi", $"Failed to log narration: {ex.Message}", "OK");
            }
        }
    }
}

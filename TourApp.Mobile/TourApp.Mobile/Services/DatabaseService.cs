using Microsoft.Data.SqlClient;
using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString =
     "Server=10.0.2.2\\SQLEXPRESS,1433;Database=TourAppDB;User Id=sa;Password=123456;TrustServerCertificate=True;";

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch (Exception ex)
            {
                await App.Current.MainPage.DisplayAlert("Lỗi chi tiết", ex.Message, "OK");
                return false;
            }
        }

        public async Task<List<POI>> GetAllPOIsAsync()
        {
            var list = new List<POI>();
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand("SELECT * FROM POI WHERE IsActive=1 ORDER BY Priority", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                list.Add(new POI
                {
                    PoiId = reader.GetInt32(0),
                    PoiName = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    Address = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Latitude = reader.GetDouble(4),
                    Longitude = reader.GetDouble(5),
                    Radius = reader.GetDouble(6),
                    Priority = reader.GetInt32(7),
                    IsActive = reader.GetBoolean(10)
                });
            }
            return list;
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand(
                "SELECT * FROM Audio WHERE PoiId=@PoiId AND Language=@Lang AND IsActive=1", conn);
            cmd.Parameters.AddWithValue("@PoiId", poiId);
            cmd.Parameters.AddWithValue("@Lang", lang);
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Audio
                {
                    AudioId = reader.GetInt32(0),
                    PoiId = reader.GetInt32(1),
                    Language = reader.GetString(2),
                    AudioPath = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    ScriptText = reader.IsDBNull(4) ? "" : reader.GetString(4),
                    IsActive = reader.GetBoolean(6)
                };
            }
            return null;
        }

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            var cmd = new SqlCommand(
                @"INSERT INTO NarrationLog (DeviceId, PoiId, AudioId, TriggerType)
                  VALUES (@DeviceId, @PoiId, @AudioId, @TriggerType)", conn);
            cmd.Parameters.AddWithValue("@DeviceId", DeviceInfo.Current.Name);
            cmd.Parameters.AddWithValue("@PoiId", poiId);
            cmd.Parameters.AddWithValue("@AudioId", (object?)audioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@TriggerType", triggerType);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
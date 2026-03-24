using TourApp.Mobile.Models;

namespace TourApp.Mobile.Services
{
    public class DatabaseService
    {
        // TODO: thay bằng REST API sau khi bạn kia làm xong backend
        private const bool USE_MOCK_DATA = true;

        public async Task<bool> TestConnectionAsync()
        {
            // Tạm thời luôn trả về true, không kết nối DB thật
            await Task.CompletedTask;
            return true;
        }

        public async Task<List<POI>> GetAllPOIsAsync()
        {
            await Task.CompletedTask;
            return new List<POI>
            {
                new POI { PoiId=1, PoiName="Ốc Oanh",
                    Description="Quán Ốc Oanh nổi tiếng nhất nhì khu phố Vĩnh Khánh với các món ốc móng tay mít hải sản nướng mỡ hành thơm lừng.",
                    Latitude=10.75960, Longitude=106.70180, Radius=30, Priority=1, IsActive=true, Rating=4.8,
                    ImageUrl="https://images.unsplash.com/photo-1583417319070-4a69db38a482?q=80&w=600" },

                new POI { PoiId=2, PoiName="Lẩu Bò Khu Vực",
                    Description="Điểm dừng chân tuyệt vời cho món lẩu bò đậm đà, nóng hổi nhâm nhi trong buổi tối Sài Gòn.",
                    Latitude=10.75890, Longitude=106.70050, Radius=25, Priority=1, IsActive=true, Rating=4.6,
                    ImageUrl="https://images.unsplash.com/photo-1555939594-58d7cb561ad1?q=80&w=600" },

                new POI { PoiId=3, PoiName="Trà Sữa Vĩnh Khánh",
                    Description="Giải khát ngay với ly trà sữa thơm mát sau khi thưởng thức các món ăn mặn.",
                    Latitude=10.75820, Longitude=106.69910, Radius=20, Priority=2, IsActive=true, Rating=4.3,
                    ImageUrl="https://images.unsplash.com/photo-1558138838-76294611cb1e?q=80&w=600" }
            };
        }

        public async Task<Audio?> GetAudioByPoiAsync(int poiId, string lang = "vi")
        {
            await Task.CompletedTask;
            return null; // chưa có audio, dùng TTS sau
        }

        public async Task LogNarrationAsync(int poiId, int? audioId, string triggerType)
        {
            await Task.CompletedTask;
            // TODO: gọi API log sau
        }
    }
}
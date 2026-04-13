using System.ComponentModel.DataAnnotations;

namespace TourApp.CMS.Models
{
    public class Tour
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [Range(0.0000001, double.MaxValue, ErrorMessage = "Giá vé phải lớn hơn 0")]
        public double Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Thoi luong khong duoc am")]
        public int Duration { get; set; }

        public string Destination { get; set; } = string.Empty;

        [Range(0, int.MaxValue, ErrorMessage = "So khach khong duoc am")]
        public int MaxParticipants { get; set; }

        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } = true;
        public string SearchKeywords { get; set; } = string.Empty;

        /// <summary>Mã nghiệp vụ TR-1, TR-2... (Business Key, VARCHAR).</summary>
        public string? Code { get; set; }

        /// <summary>Mã hiển thị đầy đủ (VD: TR-1).</summary>
        public string DisplayCode => string.IsNullOrEmpty(Code) ? $"TR-{Id}" : Code;
    }
}

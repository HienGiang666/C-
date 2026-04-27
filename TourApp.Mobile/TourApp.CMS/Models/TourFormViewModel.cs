using System.ComponentModel.DataAnnotations;

namespace TourApp.CMS.Models;

public class TourFormViewModel
{
    public Tour Tour { get; set; } = new();

    [Range(1, 50, ErrorMessage = "Số quán / điểm dừng phải từ 1 đến 50")]
    public int RestaurantCount { get; set; } = 1;

    /// <summary>Thứ tự POI trong tour (cùng thứ tự hiển thị trên mobile).</summary>
    public List<int> StopPoiIds { get; set; } = new();

    /// <summary>Bản dịch mô tả tour theo ngôn ngữ.</summary>
    public List<TourTranslation> Translations { get; set; } = new();
}

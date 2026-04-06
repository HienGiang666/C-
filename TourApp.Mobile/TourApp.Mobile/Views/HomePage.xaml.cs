using System.Collections.ObjectModel;

namespace TourApp.Mobile.Views;

public partial class HomePage : ContentPage
{
    public ObservableCollection<MockItem> MockTours { get; set; } = new();
    public ObservableCollection<MockItem> MockPois { get; set; } = new();

    public HomePage()
    {
        InitializeComponent();
        
        MockTours.Add(new MockItem { Name = "Tour Ốc Vĩnh Khánh", Summary = "5 điểm • 3.2km" });
        MockTours.Add(new MockItem { Name = "Tour Nướng Quận 1", Summary = "4 điểm • 2.8km" });

        MockPois.Add(new MockItem { Name = "Ốc Xiên Quán", Summary = "⭐ 4.5" });
        MockPois.Add(new MockItem { Name = "Bánh Tráng Trộn", Summary = "⭐ 4.6" });
        MockPois.Add(new MockItem { Name = "Lẩu Bò", Summary = "⭐ 4.9" });

        TourCollectionView.ItemsSource = MockTours;
        PoiCollectionView.ItemsSource = MockPois;
    }

    private async void OnXemThemTourTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///TourPage");
    }

    private async void OnXemThemPoiTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///POIPage");
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Test QR Scanner", "Nhập POI ID để giả lập quét mã QR thành công:", "Quét", "Huỷ", "Ví dụ: 3");
        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int currentId))
        {
            await Shell.Current.GoToAsync("///MapPage"); // Jump to Map and process QR
        }
    }
}

public class MockItem
{
    public string? Name { get; set; }
    public string? Summary { get; set; }
}

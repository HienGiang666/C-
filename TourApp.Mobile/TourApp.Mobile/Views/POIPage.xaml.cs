using System.Collections.ObjectModel;

namespace TourApp.Mobile.Views;

public partial class POIPage : ContentPage
{
    public ObservableCollection<MockItem> MockPois { get; set; } = new();

    public POIPage()
    {
        InitializeComponent();
        
        MockPois.Add(new MockItem { Name = "Ốc Xiên Quán", Summary = "⭐ 4.5 • 9.5km" });
        MockPois.Add(new MockItem { Name = "Nướng Nguyễn...", Summary = "⭐ 4.8 • 5.2km" });
        MockPois.Add(new MockItem { Name = "Bánh Tráng Trộn", Summary = "⭐ 4.6 • 3.1km" });
        MockPois.Add(new MockItem { Name = "Quán Cô Ba", Summary = "⭐ 4.7 • 6.8km" });

        PoiCollectionView.ItemsSource = MockPois;
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Test QR Scanner", "Nhập POI ID để giả lập quét mã QR thành công:", "Quét", "Huỷ", "Ví dụ: 3");
        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int currentId))
        {
            await Shell.Current.GoToAsync("///MapPage"); 
        }
    }
}

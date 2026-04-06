using System.Collections.ObjectModel;

namespace TourApp.Mobile.Views;

public partial class TourPage : ContentPage
{
    public ObservableCollection<MockItem> MockTours { get; set; } = new();

    public TourPage()
    {
        InitializeComponent();
        
        MockTours.Add(new MockItem { Name = "Tour Ốc Vĩnh Khánh", Summary = "5 điểm • 3.2km" });
        MockTours.Add(new MockItem { Name = "Tour Nướng Quận 1", Summary = "4 điểm • 2.8km" });

        TourCollectionView.ItemsSource = MockTours;
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        string result = await DisplayPromptAsync("Test QR Scanner", "Nhập POI ID để giả lập quét mã QR thành công:", "Quét", "Huỷ", "Ví dụ: 3");
        if (!string.IsNullOrEmpty(result) && int.TryParse(result, out int currentId))
        {
            await Shell.Current.GoToAsync("///MapPage"); 
        }
    }

    private async void OnStartTourClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///MapPage");
    }
}

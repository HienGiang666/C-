using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class HomePage : ContentPage
{
    public ObservableCollection<MockItem> MockTours { get; set; } = new();
    public ObservableCollection<MockItem> MockPois { get; set; } = new();
    
    private List<POI>? _allPois;
    private readonly ApiService _apiService;

    public HomePage()
    {
        InitializeComponent();
        
        _apiService = new ApiService();
        
        MockTours.Add(new MockItem { Name = "Tour Ốc Vĩnh Khánh", Summary = "5 điểm • 3.2km" });
        MockTours.Add(new MockItem { Name = "Tour Nướng Quận 1", Summary = "4 điểm • 2.8km" });

        MockPois.Add(new MockItem { Name = "Ốc Xiên Quán", Summary = "⭐ 4.5", PoiId = 1 });
        MockPois.Add(new MockItem { Name = "Bánh Tráng Trộn", Summary = "⭐ 4.6", PoiId = 2 });
        MockPois.Add(new MockItem { Name = "Lẩu Bò", Summary = "⭐ 4.9", PoiId = 3 });

        TourCollectionView.ItemsSource = MockTours;
        PoiCollectionView.ItemsSource = MockPois;
        
        // Load POIs from API
        _ = LoadPoisAsync();
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Update username label with current user's name
        if (AuthService.CurrentUser != null)
        {
            UserNameLabel.Text = AuthService.CurrentUser.DisplayName;
        }
        else
        {
            UserNameLabel.Text = "Tour Quận 4";
        }
    }
    
    private async Task LoadPoisAsync()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            _allPois = await _apiService.GetAllPOIsAsync();
            
            if (_allPois?.Any() == true)
            {
                // Update MockPois with real data
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MockPois.Clear();
                    foreach (var poi in _allPois.Take(10))
                    {
                        MockPois.Add(new MockItem 
                        { 
                            Name = poi.PoiName, 
                            Summary = $"⭐ {poi.Rating:F1}",
                            PoiId = poi.PoiId,
                            Latitude = poi.Latitude,
                            Longitude = poi.Longitude
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] LoadPoisAsync error: {ex.Message}");
        }
    }

    private async void OnSearchBarTapped(object sender, EventArgs e)
    {
        // Focus the search entry when the search bar frame is tapped
        SearchEntry.Focus();
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;
        
        await PerformSearch(query);
    }
    
    private async Task PerformSearch(string query)
    {
        // Search in POIs
        var matchingPois = _allPois?.Where(p => 
            p.PoiName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        // Search in Tours (mock data for now)
        var matchingTours = MockTours.Where(t =>
            t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if ((matchingPois?.Any() != true) && !matchingTours.Any())
        {
            await DisplayAlert("Kết quả tìm kiếm", $"Không tìm thấy quán ăn hoặc tour nào cho '{query}'", "OK");
            return;
        }
        
        // Build result message
        var resultMessage = "";
        if (matchingPois?.Any() == true)
        {
            resultMessage += $"🍽️ Tìm thấy {matchingPois.Count} quán ăn:\n";
            foreach (var poi in matchingPois.Take(5))
            {
                resultMessage += $"• {poi.PoiName}\n";
            }
        }
        
        if (matchingTours.Any())
        {
            if (!string.IsNullOrEmpty(resultMessage)) resultMessage += "\n";
            resultMessage += $"🚩 Tìm thấy {matchingTours.Count} tour:\n";
            foreach (var tour in matchingTours)
            {
                resultMessage += $"• {tour.Name}\n";
            }
        }
        
        var action = await DisplayActionSheet($"Kết quả cho '{query}'", "Huỷ", null, 
            matchingPois?.Any() == true ? "Xem quán ăn" : null,
            matchingTours.Any() ? "Xem tour" : null);
            
        if (action == "Xem quán ăn" && matchingPois?.Any() == true)
        {
            // Navigate to POI page with search filter
            await Shell.Current.GoToAsync("///POIPage");
        }
        else if (action == "Xem tour" && matchingTours.Any())
        {
            // Navigate to Tour page
            await Shell.Current.GoToAsync("///TourPage");
        }
    }

    private async void OnNuongTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("nướng", "Nướng");
    }

    private async void OnLauTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("lẩu", "Lẩu");
    }

    private async void OnOcTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("ốc", "Ốc");
    }

    private async void OnAnVatTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("ăn vặt", "Ăn vặt");
    }
    
    private async Task FilterPoisByCategory(string keyword, string categoryName)
    {
        // Filter POIs by keyword in name or description
        var filteredPois = _allPois?.Where(p =>
            p.PoiName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        // Also check mock data if no API data
        if (filteredPois?.Any() != true)
        {
            var mockFiltered = MockPois.Where(p =>
                p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            if (mockFiltered.Any())
            {
                await DisplayAlert(categoryName, $"Tìm thấy {mockFiltered.Count} quán {categoryName}:\n" + 
                    string.Join("\n", mockFiltered.Select(p => $"• {p.Name}")), "Xem tất cả", "Đóng");
            }
            else
            {
                await DisplayAlert(categoryName, $"Không tìm thấy quán {categoryName} nào.", "OK");
                return;
            }
        }
        else
        {
            var result = await DisplayAlert(categoryName, 
                $"Tìm thấy {filteredPois.Count} quán {categoryName}\n" +
                string.Join("\n", filteredPois.Take(5).Select(p => $"• {p.PoiName}")),
                "Xem tất cả", "Đóng");
                
            if (result)
            {
                await Shell.Current.GoToAsync("///POIPage");
            }
        }
    }

    private async void OnTourSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is MockItem selectedTour)
        {
            // Clear selection
            TourCollectionView.SelectedItem = null;
            
            // Navigate to Tour page
            await Shell.Current.GoToAsync("///TourPage");
        }
    }

    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is MockItem selectedPoi)
        {
            // Clear selection
            PoiCollectionView.SelectedItem = null;
            
            // Navigate to Map page with POI ID
            if (selectedPoi.PoiId > 0)
            {
                await Shell.Current.GoToAsync($"///MapPage?poiId={selectedPoi.PoiId}");
            }
            else
            {
                // If no ID, just go to Map page
                await Shell.Current.GoToAsync("///MapPage");
            }
        }
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
        // Navigate to real QR scanner page
        await Shell.Current.GoToAsync("QRScannerPage");
    }
}

public class MockItem
{
    public string? Name { get; set; }
    public string? Summary { get; set; }
    public int PoiId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

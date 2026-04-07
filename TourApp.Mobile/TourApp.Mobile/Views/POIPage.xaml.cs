using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class POIPage : ContentPage
{
    public ObservableCollection<POI> POIs { get; set; } = new();
    private List<POI>? _allPois;
    private readonly ApiService _apiService;

    public POIPage()
    {
        InitializeComponent();
        
        _apiService = new ApiService();
        PoiCollectionView.ItemsSource = POIs;
        
        // Load POIs from API
        _ = LoadPoisAsync();
    }
    
    private async Task LoadPoisAsync()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            _allPois = await _apiService.GetAllPOIsAsync();
            
            if (_allPois?.Any() == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    POIs.Clear();
                    foreach (var poi in _allPois.Where(p => p.IsActive))
                    {
                        POIs.Add(poi);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIPage] LoadPoisAsync error: {ex.Message}");
        }
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        // Navigate to real QR scanner
        await Shell.Current.GoToAsync("QRScannerPage");
    }
    
    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        if (sender is Entry searchEntry)
        {
            var query = searchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                // Reset to show all
                if (_allPois != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        POIs.Clear();
                        foreach (var poi in _allPois.Where(p => p.IsActive))
                        {
                            POIs.Add(poi);
                        }
                    });
                }
                return;
            }
            
            // Filter POIs
            var filtered = _allPois?.Where(p => 
                p.PoiName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                p.Address.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            if (filtered?.Any() == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    POIs.Clear();
                    foreach (var poi in filtered)
                    {
                        POIs.Add(poi);
                    }
                });
            }
            else
            {
                await DisplayAlert("Kết quả", $"Không tìm thấy quán ăn nào cho '{query}'", "OK");
            }
        }
    }
    
    private void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is POI selectedPoi)
        {
            // Clear selection
            PoiCollectionView.SelectedItem = null;
            
            // Navigate to MapPage with POI ID
            Shell.Current.GoToAsync($"///MapPage?poiId={selectedPoi.PoiId}");
        }
    }
}

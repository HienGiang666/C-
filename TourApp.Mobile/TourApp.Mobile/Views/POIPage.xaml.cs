using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class POIPage : ContentPage
{
    // Sử dụng OnPropertyChanged có sẵn của BindableObject (không cần khai báo lại)
    
    public ObservableCollection<POI> POIs { get; set; } = new();
    private List<POI>? _allPois;
    private readonly ApiService _apiService;
    
    // Localized properties
    private string _pageTitle = "";
    private string _searchPlaceholder = "";
    
    public string PageTitle { get => _pageTitle; set { _pageTitle = value; OnPropertyChanged(nameof(PageTitle)); } }
    public string SearchPlaceholder { get => _searchPlaceholder; set { _searchPlaceholder = value; OnPropertyChanged(nameof(SearchPlaceholder)); } }

    public POIPage()
    {
        InitializeComponent();
        
        _apiService = new ApiService();
        PoiCollectionView.ItemsSource = POIs;
        
        // Subscribe to language changes
        LanguageService.LanguageChanged += OnLanguageChanged;
        
        // Initialize localized text
        UpdateLocalizedText();
        
        BindingContext = this;
        
        // Load POIs from API
        _ = LoadPoisAsync();
    }
    
    ~POIPage()
    {
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }
    
    private void OnLanguageChanged(object? sender, string newLang)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateLocalizedText();
        });
    }
    
    private void UpdateLocalizedText()
    {
        PageTitle = LanguageService.GetString("POITitle");
        SearchPlaceholder = LanguageService.GetString("POISearchPlaceholder");
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLocalizedText();
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
                await DisplayAlert(LanguageService.GetString("SearchResults"), 
                    LanguageService.GetString("NoResults", query), 
                    LanguageService.GetString("OK"));
            }
        }
    }
    
    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is POI selectedPoi)
        {
            // Clear selection
            PoiCollectionView.SelectedItem = null;
            
            // Navigate to MapPage with POI ID
            await Shell.Current.GoToAsync($"///MapPage?poiId={selectedPoi.PoiId}");
        }
    }
}

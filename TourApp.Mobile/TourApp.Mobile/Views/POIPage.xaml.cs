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
        
        // Offline banner
        NetworkService.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner();

        // Load POIs from API
        _ = LoadPoisAsync();
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateOfflineBanner();
            if (isOnline) _ = LoadPoisAsync();
        });
    }

    private void UpdateOfflineBanner()
    {
        OfflineBanner.IsVisible = !NetworkService.IsConnected;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try
        {
            LanguageService.LanguageChanged -= OnLanguageChanged;
            NetworkService.ConnectivityChanged -= OnConnectivityChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIPage] OnDisappearing error: {ex.Message}");
        }
    }
    
    private void OnLanguageChanged(object? sender, string newLang)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                UpdateLocalizedText();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[POIPage] OnLanguageChanged error: {ex.Message}");
            }
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
                var activePois = _allPois.Where(p => p.IsActive).ToList();

                // Pre-download ảnh trước khi hiển thị
                await ImageCacheService.PreloadAsync(activePois.Select(p => p.ImageUrl));

                // Thay ImageUrl bằng local path
                foreach (var poi in activePois)
                {
                    var localPath = await ImageCacheService.GetLocalPathAsync(poi.ImageUrl);
                    if (localPath != null)
                        poi.ImageUrl = localPath;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    POIs.Clear();
                    foreach (var poi in activePois)
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
        await Shell.Current.GoToAsync("QRScannerPage");
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 1)
        {
            SearchDropdown.IsVisible = false;
            return;
        }

        var results = _allPois?
            .Where(p => p.Name != null && p.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(6)
            .ToList();

        if (results != null && results.Count > 0)
        {
            SearchResultsView.ItemsSource = results;
            SearchDropdown.IsVisible = true;
        }
        else
        {
            SearchDropdown.IsVisible = false;
        }
    }

    private async void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is POI selectedPoi)
        {
            SearchDropdown.IsVisible = false;
            SearchEntry.Text = selectedPoi.Name;
            SearchResultsView.SelectedItem = null;
            await Shell.Current.GoToAsync($"//MapPage?poiId={selectedPoi.Id}");
        }
    }

    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        if (sender is not Entry searchEntry) return;
        
        var query = searchEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            // Reset to show all
            if (_allPois != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        POIs.Clear();
                        foreach (var poi in _allPois.Where(p => p.IsActive))
                        {
                            POIs.Add(poi);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[POIPage] Search reset error: {ex.Message}");
                    }
                });
            }
            return;
        }
        
        try
        {
            // Filter POIs
            var filtered = _allPois?.Where(p => 
                p.Name?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                p.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                p.Address?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
                
            if (filtered?.Any() == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    try
                    {
                        POIs.Clear();
                        foreach (var poi in filtered)
                        {
                            POIs.Add(poi);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[POIPage] Search filter error: {ex.Message}");
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
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[POIPage] Search error: {ex.Message}");
        }
    }
    
    private async void OnPoiSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is POI selectedPoi)
        {
            // Clear selection
            PoiCollectionView.SelectedItem = null;
            
            // Navigate to MapPage with POI ID
            await Shell.Current.GoToAsync($"//MapPage?poiId={selectedPoi.Id}");
        }
    }
}

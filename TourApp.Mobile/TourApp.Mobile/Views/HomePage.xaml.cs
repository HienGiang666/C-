using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class HomePage : ContentPage
{
    // Sử dụng OnPropertyChanged có sẵn của BindableObject (không cần khai báo lại)
    
    public ObservableCollection<MockItem> MockTours { get; set; } = new();
    public ObservableCollection<MockItem> MockPois { get; set; } = new();
    
    private List<POI>? _allPois;
    private List<Tour>? _allTours;
    private readonly ApiService _apiService;
    
    // Localized properties for XAML binding
    private string _homeTitle = "";
    private string _homeSubtitle = "";
    private string _searchPlaceholder = "";
    private string _categoriesTitle = "";
    private string _popularPOIsTitle = "";
    private string _toursTitle = "";
    private string _viewAllText = "";
    private string _scanQRText = "";
    
    public string HomeTitle { get => _homeTitle; set { _homeTitle = value; OnPropertyChanged(nameof(HomeTitle)); } }
    public string HomeSubtitle { get => _homeSubtitle; set { _homeSubtitle = value; OnPropertyChanged(nameof(HomeSubtitle)); } }
    public string SearchPlaceholder { get => _searchPlaceholder; set { _searchPlaceholder = value; OnPropertyChanged(nameof(SearchPlaceholder)); } }
    public string CategoriesTitle { get => _categoriesTitle; set { _categoriesTitle = value; OnPropertyChanged(nameof(CategoriesTitle)); } }
    public string PopularPOIsTitle { get => _popularPOIsTitle; set { _popularPOIsTitle = value; OnPropertyChanged(nameof(PopularPOIsTitle)); } }
    public string ToursTitle { get => _toursTitle; set { _toursTitle = value; OnPropertyChanged(nameof(ToursTitle)); } }
    public string ViewAllText { get => _viewAllText; set { _viewAllText = value; OnPropertyChanged(nameof(ViewAllText)); } }
    public string ScanQRText { get => _scanQRText; set { _scanQRText = value; OnPropertyChanged(nameof(ScanQRText)); } }

    public HomePage()
    {
        InitializeComponent();
        
        _apiService = new ApiService();
        
        // Initialize localized text
        UpdateLocalizedText();
        
        // Subscribe to language changes
        LanguageService.LanguageChanged += OnLanguageChanged;
        
        // Dữ liệu sẽ được load từ API trong LoadDataAsync()

        TourCollectionView.ItemsSource = MockTours;
        PoiCollectionView.ItemsSource = MockPois;
        
        // Load POIs and Tours from API
        _ = LoadDataAsync();
        
        // Offline banner
        NetworkService.ConnectivityChanged += OnConnectivityChanged;
        UpdateOfflineBanner();
        
        BindingContext = this;
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
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnDisappearing error: {ex.Message}");
        }
    }

    private void OnConnectivityChanged(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateOfflineBanner();
            if (isOnline)
            {
                // Có mạng lại → reload data mới
                _ = LoadDataAsync();
            }
        });
    }

    private void UpdateOfflineBanner()
    {
        OfflineBanner.IsVisible = !NetworkService.IsConnected;
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
                System.Diagnostics.Debug.WriteLine($"[HomePage] OnLanguageChanged error: {ex.Message}");
            }
        });
    }
    
    private void UpdateLocalizedText()
    {
        HomeTitle = LanguageService.GetString("HomeTitle");
        HomeSubtitle = LanguageService.GetString("HomeSubtitle");
        SearchPlaceholder = LanguageService.GetString("SearchPlaceholder");
        CategoriesTitle = LanguageService.GetString("Categories");
        PopularPOIsTitle = LanguageService.GetString("PopularPOIs");
        ToursTitle = LanguageService.GetString("Tours");
        ViewAllText = LanguageService.GetString("ViewAll");
        ScanQRText = LanguageService.GetString("ScanQR");
        if (WelcomeLabel != null)
            WelcomeLabel.Text = LanguageService.GetString("Welcome") + ",";
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        try
        {
            // Update username label with current user's name
            if (UserNameLabel == null) return;
            
            if (AuthService.CurrentUser != null)
            {
                UserNameLabel.Text = AuthService.CurrentUser.DisplayName;
            }
            else
            {
                UserNameLabel.Text = LanguageService.GetString("AppName");
            }
            
            // Refresh localized text
            UpdateLocalizedText();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] OnAppearing error: {ex.Message}");
        }
    }
    
    private async Task LoadDataAsync()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            
            // Đồng bộ bản dịch UI từ server (fire-and-forget, không block UI)
            _ = LanguageService.SyncFromServerAsync();
            
            // Load POIs
            _allPois = await _apiService.GetAllPOIsAsync();
            System.Diagnostics.Debug.WriteLine($"[HomePage] Loaded {_allPois?.Count ?? 0} POIs");
            if (_allPois?.Any() == true)
            {
                var topPois = _allPois.Take(10).ToList();
                // Pre-download tất cả ảnh
                await ImageCacheService.PreloadAsync(topPois.Select(p => p.ImageUrl));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MockPois.Clear();
                    foreach (var poi in topPois)
                    {
                        var localPath = ImageCacheService.GetLocalPathAsync(poi.ImageUrl).Result;
                        System.Diagnostics.Debug.WriteLine($"[HomePage] POI {poi.Id}: {poi.Name}, local={localPath ?? "null"}");
                        MockPois.Add(new MockItem 
                        { 
                            Name = poi.Name ?? "Unknown", 
                            Summary = $"⭐ {poi.Rating:F1}",
                            ImageUrl = localPath,
                            PoiId = poi.Id,
                            Latitude = poi.Latitude,
                            Longitude = poi.Longitude
                        });
                    }
                });
            }

            // Load Tours
            _allTours = await _apiService.GetAllToursAsync();
            if (_allTours?.Any() == true)
            {
                var topTours = _allTours.Take(5).ToList();
                await ImageCacheService.PreloadAsync(topTours.Select(t => t.ImageUrl));

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MockTours.Clear();
                    foreach (var tour in topTours)
                    {
                        var localPath = ImageCacheService.GetLocalPathAsync(tour.ImageUrl).Result;
                        MockTours.Add(new MockItem
                        {
                            Name = tour.Name ?? "Tour",
                            Summary = $"{tour.PoiCount} điểm",
                            ImageUrl = localPath,
                            TourId = tour.Id
                        });
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[HomePage] LoadDataAsync error: {ex.Message}");
        }
    }

    private void OnSearchBarTapped(object sender, EventArgs e)
    {
        // Focus the search entry when the search bar frame is tapped
        SearchEntry.Focus();
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim();
        if (string.IsNullOrEmpty(query) || query.Length < 1)
        {
            SearchDropdown.IsVisible = false;
            return;
        }

        // Filter local POIs matching query
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

            // Navigate to MapPage with POI ID
            await Shell.Current.GoToAsync($"//MapPage?poiId={selectedPoi.Id}");
        }
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
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        // Search in Tours
        var matchingTours = _allTours?.Where(t =>
            !string.IsNullOrEmpty(t.Name) && t.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        if ((matchingPois?.Any() != true) && (matchingTours?.Any() != true))
        {
            await DisplayAlert(LanguageService.GetString("SearchResults"), 
                LanguageService.GetString("NoResults", query), 
                LanguageService.GetString("OK"));
            return;
        }
        
        // Build result message
        var resultMessage = "";
        if (matchingPois?.Any() == true)
        {
            resultMessage += $"🍽️ {LanguageService.GetString("RestaurantsFound", matchingPois.Count, "")}:\n";
            foreach (var poi in matchingPois.Take(5))
            {
                resultMessage += $"• {poi.Name}\n";
            }
        }
        
        if (matchingTours?.Any() == true)
        {
            if (!string.IsNullOrEmpty(resultMessage)) resultMessage += "\n";
            resultMessage += $"🚩 {LanguageService.GetString("Tours")}:\n";
            foreach (var tour in matchingTours)
            {
                resultMessage += $"• {tour.Name}\n";
            }
        }
        
        var action = await DisplayActionSheet(LanguageService.GetString("SearchResults"), 
            LanguageService.GetString("Cancel"), null, 
            matchingPois?.Any() == true ? LanguageService.GetString("ViewRestaurants") : null,
            matchingTours.Any() ? LanguageService.GetString("ViewTours") : null);
            
        if (action == LanguageService.GetString("ViewRestaurants") && matchingPois?.Any() == true)
        {
            // Navigate to POI page with search filter
            await Shell.Current.GoToAsync("//POIPage");
        }
        else if (action == LanguageService.GetString("ViewTours") && matchingTours.Any())
        {
            // Navigate to Tour page
            await Shell.Current.GoToAsync("//TourPage");
        }
    }

    private async void OnNuongTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("nướng", LanguageService.GetString("CategoryGrill"));
    }

    private async void OnLauTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("lẩu", LanguageService.GetString("CategoryHotpot"));
    }

    private async void OnOcTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("ốc", LanguageService.GetString("CategorySeafood"));
    }

    private async void OnAnVatTapped(object sender, EventArgs e)
    {
        await FilterPoisByCategory("ăn vặt", LanguageService.GetString("CategorySnacks"));
    }
    
    private async Task FilterPoisByCategory(string keyword, string categoryName)
    {
        // Filter POIs by keyword in name or description
        var filteredPois = _allPois?.Where(p =>
            p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            p.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            .ToList();
            
        // Also check mock data if no API data
        if (filteredPois?.Any() != true)
        {
            var mockFiltered = MockPois.Where(p =>
                !string.IsNullOrEmpty(p.Name) && p.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            if (mockFiltered.Any())
            {
                await DisplayAlert(categoryName, 
                    LanguageService.GetString("RestaurantsFound", mockFiltered.Count, categoryName), 
                    LanguageService.GetString("ViewAllRestaurants"), 
                    LanguageService.GetString("Close"));
            }
            else
            {
                await DisplayAlert(categoryName, 
                    LanguageService.GetString("NoPOIFound"), 
                    LanguageService.GetString("OK"));
                return;
            }
        }
        else
        {
            var result = await DisplayAlert(categoryName, 
                LanguageService.GetString("RestaurantsFound", filteredPois.Count, categoryName),
                LanguageService.GetString("ViewAllRestaurants"), 
                LanguageService.GetString("Close"));
                
            if (result)
            {
                await Shell.Current.GoToAsync("//POIPage");
            }
        }
    }

    private async void OnTourSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is MockItem selectedTour)
        {
            // Clear selection
            TourCollectionView.SelectedItem = null;

            if (selectedTour.TourId > 0)
            {
                await Shell.Current.GoToAsync($"//MapPage?tourId={selectedTour.TourId}");
                return;
            }

            await Shell.Current.GoToAsync("//MapPage");
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
                await Shell.Current.GoToAsync($"//MapPage?poiId={selectedPoi.PoiId}");
            }
            else
            {
                // If no ID, just go to Map page
                await Shell.Current.GoToAsync("//MapPage");
            }
        }
    }

    private async void OnXemThemTourTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//TourPage");
    }

    private async void OnXemThemPoiTapped(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//POIPage");
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
    public string? ImageUrl { get; set; }
    public int PoiId { get; set; }
    public int TourId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

public partial class TourPage : ContentPage
{
    // Sử dụng OnPropertyChanged có sẵn của BindableObject (không cần khai báo lại)
    
    public ObservableCollection<Tour> Tours { get; set; } = new();
    private List<Tour>? _allTours;
    private readonly ApiService _apiService;
    
    // Localized properties
    private string _pageTitle = "";
    private string _searchPlaceholder = "";
    private string _startTourText = "";
    
    public string PageTitle { get => _pageTitle; set { _pageTitle = value; OnPropertyChanged(nameof(PageTitle)); } }
    public string SearchPlaceholder { get => _searchPlaceholder; set { _searchPlaceholder = value; OnPropertyChanged(nameof(SearchPlaceholder)); } }
    public string StartTourText { get => _startTourText; set { _startTourText = value; OnPropertyChanged(nameof(StartTourText)); } }

    public TourPage()
    {
        InitializeComponent();
        
        _apiService = new ApiService();
        TourCollectionView.ItemsSource = Tours;
        
        // Subscribe to language changes
        LanguageService.LanguageChanged += OnLanguageChanged;
        
        // Initialize localized text
        UpdateLocalizedText();
        
        BindingContext = this;
        
        // Load tours from API
        _ = LoadToursAsync();
    }
    
    ~TourPage()
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
        PageTitle = LanguageService.GetString("TourTitle");
        SearchPlaceholder = LanguageService.GetString("TourSearchPlaceholder");
        StartTourText = LanguageService.GetString("StartTour");
    }
    
    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateLocalizedText();
    }
    
    private async Task LoadToursAsync()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            _allTours = await _apiService.GetAllToursAsync();
            
            if (_allTours?.Any() == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Tours.Clear();
                    foreach (var tour in _allTours.Where(t => t.IsActive))
                    {
                        Tours.Add(tour);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourPage] LoadToursAsync error: {ex.Message}");
        }
    }

    private async void OnQRScanClicked(object sender, EventArgs e)
    {
        // Navigate to real QR scanner
        await Shell.Current.GoToAsync("QRScannerPage");
    }

    private async void OnStartTourClicked(object sender, EventArgs e)
    {
        // Get the tour from the binding context of the button
        if (sender is Button button && button.BindingContext is Tour selectedTour)
        {
            // Navigate to MapPage with tour ID to show the route
            await Shell.Current.GoToAsync($"///MapPage?tourId={selectedTour.Id}");
        }
    }
    
    private async void OnSearchCompleted(object sender, EventArgs e)
    {
        if (sender is Entry searchEntry)
        {
            var query = searchEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                // Reset to show all
                if (_allTours != null)
                {
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Tours.Clear();
                        foreach (var tour in _allTours.Where(t => t.IsActive))
                        {
                            Tours.Add(tour);
                        }
                    });
                }
                return;
            }
            
            // Filter tours
            var filtered = _allTours?.Where(t => 
                t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                t.Description.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();
                
            if (filtered?.Any() == true)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    Tours.Clear();
                    foreach (var tour in filtered)
                    {
                        Tours.Add(tour);
                    }
                });
            }
            else
            {
                await DisplayAlert("Kết quả", $"Không tìm thấy tour nào cho '{query}'", "OK");
            }
        }
    }
    
    private async void OnTourSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection?.FirstOrDefault() is Tour selectedTour)
        {
            // Clear selection
            TourCollectionView.SelectedItem = null;
            
            // Navigate to MapPage with tour ID
            await Shell.Current.GoToAsync($"///MapPage?tourId={selectedTour.Id}");
        }
    }
}

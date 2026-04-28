using System.Collections.ObjectModel;
using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views;

[QueryProperty(nameof(TourId), "tourId")]
public partial class TourDetailPage : ContentPage
{
    private readonly ApiService _apiService;
    private Tour? _currentTour;
    private int _tourId;

    public ObservableCollection<TourPOI> TourStops { get; set; } = new();

    public int TourId
    {
        get => _tourId;
        set
        {
            _tourId = value;
            _ = LoadTourData();
        }
    }

    public TourDetailPage()
    {
        InitializeComponent();
        _apiService = new ApiService();
        StopsCollectionView.ItemsSource = TourStops;
        LanguageService.LanguageChanged += OnLanguageChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
        LanguageService.LanguageChanged += OnLanguageChanged;
        // Refresh code-behind labels in case language changed while away
        RefreshLocalizedLabels();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string newLang)
    {
        MainThread.BeginInvokeOnMainThread(() => RefreshLocalizedLabels());
    }

    private void RefreshLocalizedLabels()
    {
        if (_currentTour == null) return;
        try
        {
            ParticipantsLabel.Text = LanguageService.GetString("MaxPeople", _currentTour.MaxParticipants);
            TourDescriptionLabel.Text = _currentTour.GetLocalizedDescription(LanguageService.CurrentLanguage);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourDetailPage] RefreshLocalizedLabels error: {ex.Message}");
        }
    }

    private async Task LoadTourData()
    {
        try
        {
            await ApiService.AutoDiscoverApiAsync();
            _currentTour = await _apiService.GetTourByIdAsync(TourId);

            if (_currentTour != null)
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TourNameLabel.Text = _currentTour.Name;
                    PriceLabel.Text = $"{_currentTour.Price:N0} đ";
                    DurationLabel.Text = _currentTour.Duration.ToString();
                    ParticipantsLabel.Text = LanguageService.GetString("MaxPeople", _currentTour.MaxParticipants);
                    TourDescriptionLabel.Text = _currentTour.GetLocalizedDescription(LanguageService.CurrentLanguage);
                    
                    // Load tour image with fallback
                    if (!string.IsNullOrEmpty(_currentTour.ImageUrl))
                    {
                        var imageUrl = _currentTour.ImageUrl.StartsWith("http") 
                            ? _currentTour.ImageUrl 
                            : ApiService.BaseUrl + _currentTour.ImageUrl;
                        System.Diagnostics.Debug.WriteLine($"[TourDetailPage] Loading image: {imageUrl}");
                        TourImage.Source = imageUrl;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[TourDetailPage] No ImageUrl, using placeholder");
                        // Default placeholder from FontAwesome
                        TourImage.Source = ImageSource.FromFile("dotnet_bot.png");
                    }
                });

                // Load stops
                var stops = await _apiService.GetTourStopsAsync(TourId);
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    TourStops.Clear();
                    foreach (var stop in stops)
                    {
                        TourStops.Add(stop);
                    }
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TourDetailPage] error: {ex.Message}");
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private async void OnViewMapClicked(object sender, EventArgs e)
    {
        if (_currentTour != null)
        {
            await Shell.Current.GoToAsync($"//MapPage?tourId={_currentTour.Id}");
        }
    }

    private async void OnBookTourClicked(object sender, EventArgs e)
    {
        if (_currentTour == null) return;

        if (AuthService.IsGuestMode || !AuthService.IsLoggedIn)
        {
            var result = await DisplayAlert(
                LanguageService.GetString("LoginRequired"),
                LanguageService.GetString("GuestBookMessage"),
                LanguageService.GetString("Login"),
                LanguageService.GetString("SignUp")
            );

            if (result)
            {
                await Shell.Current.Navigation.PushAsync(new Auth.LoginPage());
            }
            else
            {
                await Shell.Current.Navigation.PushAsync(new Auth.SignUpPage());
            }
            return;
        }

        await Shell.Current.GoToAsync($"BookingPage?tourId={_currentTour.Id}");
    }

    private async void OnDownloadAudioClicked(object sender, EventArgs e)
    {
        if (TourStops == null || !TourStops.Any())
        {
            await DisplayAlert(LanguageService.GetString("Notice"), LanguageService.GetString("NoDestinations"), LanguageService.GetString("OK"));
            return;
        }

        try
        {
            int successCount = 0;
            foreach (var stop in TourStops)
            {
                if (stop.POIId > 0)
                {
                    var audio = await _apiService.GetAudioByPoiAsync(stop.POIId, LanguageService.CurrentLanguage);
                    if (audio != null && !string.IsNullOrEmpty(audio.AudioPath))
                    {
                        var audioUrl = audio.AudioPath.StartsWith("http") 
                            ? audio.AudioPath 
                            : ApiService.BaseUrl + audio.AudioPath;
                            
                        await AudioPlayerService.Instance.PrecacheAudioAsync(audioUrl);
                        successCount++;
                    }
                }
            }
            
            await DisplayAlert(LanguageService.GetString("Success"), LanguageService.GetString("DownloadAudioSuccess", successCount, TourStops.Count), LanguageService.GetString("OK"));
        }
        catch (Exception ex)
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("DownloadAudioFailed"), LanguageService.GetString("OK"));
            System.Diagnostics.Debug.WriteLine($"[DownloadAudio] error: {ex.Message}");
        }
    }
}

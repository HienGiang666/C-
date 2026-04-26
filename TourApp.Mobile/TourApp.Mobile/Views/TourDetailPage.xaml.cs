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
                    TourDescriptionLabel.Text = _currentTour.Description;
                    
                    if (!string.IsNullOrEmpty(_currentTour.ImageUrl))
                        TourImage.Source = ApiService.BaseUrl + _currentTour.ImageUrl;
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

    private async void OnViewMapClicked(object sender, EventArgs e)
    {
        if (_currentTour != null)
        {
            await Shell.Current.GoToAsync($"//MapPage?tourId={_currentTour.Id}");
        }
    }

    private async void OnBookTourClicked(object sender, EventArgs e)
    {
        if (_currentTour != null)
        {
            await Shell.Current.GoToAsync($"BookingPage?tourId={_currentTour.Id}");
        }
    }

    private async void OnDownloadAudioClicked(object sender, EventArgs e)
    {
        if (TourStops == null || !TourStops.Any())
        {
            await DisplayAlert(LanguageService.GetString("Notice"), LanguageService.GetString("NoDestinations"), LanguageService.GetString("OK"));
            return;
        }

        var btn = sender as Button;
        if (btn != null)
        {
            btn.IsEnabled = false;
            btn.Text = $"⏳ {LanguageService.GetString("Loading")}";
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
        finally
        {
            if (btn != null)
            {
                btn.IsEnabled = true;
                btn.Text = LanguageService.GetString("DownloadOfflineAudio");
            }
        }
    }
}

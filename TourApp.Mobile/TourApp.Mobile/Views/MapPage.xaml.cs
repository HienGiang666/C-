using TourApp.Mobile.Models;
using TourApp.Mobile.Services;
using System.Text.Json;
using Microsoft.Maui.Media;
using Microsoft.Maui.Devices.Sensors;

namespace TourApp.Mobile.Views;

[QueryProperty(nameof(PoiIdQuery), "poiId")]
[QueryProperty(nameof(TourIdQuery), "tourId")]
[QueryProperty(nameof(FromQRQuery), "fromQR")]
public partial class MapPage : ContentPage
{
    private readonly LocationService _locationService;
    private readonly GeofenceService _geofenceService;
    private readonly ApiService _apiService;

    private bool _isMapLoaded = false;
    private List<POI>? _pois;
    private POI? _currentPoi;
    private Location? _lastLocation;
    private Location? _lastCheckedLocation;
    private bool _isJsMapReady = false;
    private string? _pendingMapPoisJson;
    private int? _pendingPoiId = null;
    private int? _pendingTourId = null;
    private bool _triggerFromQR = false;

    // Query properties for navigation
    public string PoiIdQuery
    {
        set
        {
            if (int.TryParse(value, out int id))
            {
                _pendingPoiId = id;
                System.Diagnostics.Debug.WriteLine($"[MapPage] Received poiId from query: {id}");
            }
        }
    }
    
    public string TourIdQuery
    {
        set
        {
            if (int.TryParse(value, out int id))
            {
                _pendingTourId = id;
                System.Diagnostics.Debug.WriteLine($"[MapPage] Received tourId from query: {id}");
            }
        }
    }

    public string FromQRQuery
    {
        set
        {
            _triggerFromQR = value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
            System.Diagnostics.Debug.WriteLine($"[MapPage] Received fromQR: {_triggerFromQR}");
        }
    }

    // ----------------------------------------------------------------
    //  GOONG KEYS
    //  - MaptileKey : dùng cho goongjs.accessToken (render bản đồ tile)
    //  - RestApiKey : dùng cho rsapi.goong.io/Place (Search, Geocode)
    //    !! Hai key KHÁC NHAU. Lấy tại: https://account.goong.io → My Keys
    // ----------------------------------------------------------------
    private const string GoongMaptileKey = "2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9";
    private const string GoongRestApiKey = "FEx6pcCh7bba5bfwJC4M7truNtLS7rEAwkZQZZ8g";

    // ── Offline Map Asset Caching ──
    private static readonly string MapCacheDir = Path.Combine(FileSystem.AppDataDirectory, "map_cache");
    private const string GoongJsUrl = "https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.js";
    private const string GoongCssUrl = "https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.css";
    private const string StyleJsonUrl = "https://tiles.goong.io/assets/goong_map_web.json";
    private static string JsCachePath => Path.Combine(MapCacheDir, "goong-js.js");
    private static string CssCachePath => Path.Combine(MapCacheDir, "goong-js.css");
    private static string StyleCachePath => Path.Combine(MapCacheDir, "style.json");

    private static bool AreMapAssetsCached() =>
        File.Exists(JsCachePath) && File.Exists(CssCachePath) && File.Exists(StyleCachePath);

    private static async Task CacheMapAssetsAsync()
    {
        try
        {
            if (!NetworkService.IsConnected) return;
            if (AreMapAssetsCached()) return;

            Directory.CreateDirectory(MapCacheDir);
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

            if (!File.Exists(JsCachePath))
            {
                var js = await client.GetStringAsync(GoongJsUrl);
                await File.WriteAllTextAsync(JsCachePath, js);
                System.Diagnostics.Debug.WriteLine($"[MapAssetCache] JS cached ({js.Length} chars)");
            }
            if (!File.Exists(CssCachePath))
            {
                var css = await client.GetStringAsync(GoongCssUrl);
                await File.WriteAllTextAsync(CssCachePath, css);
                System.Diagnostics.Debug.WriteLine($"[MapAssetCache] CSS cached ({css.Length} chars)");
            }
            if (!File.Exists(StyleCachePath))
            {
                var style = await client.GetStringAsync(StyleJsonUrl);
                await File.WriteAllTextAsync(StyleCachePath, style);
                System.Diagnostics.Debug.WriteLine($"[MapAssetCache] Style JSON cached ({style.Length} chars)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapAssetCache] Error: {ex.Message}");
        }
    }
    // ─────────────────────────────────

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HttpClient _http = new();

    public MapPage()
    {
        try
        {
            InitializeComponent();
            
            var services = IPlatformApplication.Current?.Services;
            _locationService = services?.GetService<LocationService>() ?? new LocationService();
            _apiService = services?.GetService<ApiService>() ?? new ApiService();
            _geofenceService = services?.GetService<GeofenceService>() ?? new GeofenceService(_apiService);

            // Đồng bộ ngôn ngữ từ LanguageService để TTS đúng ngôn ngữ user đã chọn
            _geofenceService.CurrentLanguage = LanguageService.CurrentLanguage;
            System.Diagnostics.Debug.WriteLine($"[MapPage] Initialized with language: {_geofenceService.CurrentLanguage}");

            _locationService.LocationChanged += OnLocationChanged;
            _geofenceService.PoiTriggered += OnPoiTriggered;
            _geofenceService.HighlightRequested += (_, poiId) =>
            {
                if (_isJsMapReady)
                    MapWebView.Eval($"highlightPoi({poiId});");
            };
#if ANDROID
            // Enable foreground service for better background tracking on Android
            _locationService.SetUseForegroundService(true);
#endif

            // Subscribe to language changes to refresh POI description
            LanguageService.LanguageChanged += OnLanguageChanged;

            // Offline banner
            NetworkService.ConnectivityChanged += OnConnectivityChanged;

            // Enable aggressive WebView caching for offline map
            MapWebView.HandlerChanged += OnWebViewHandlerChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] CONSTRUCTOR ERROR: {ex}");
            // Fallback
            _locationService ??= new LocationService();
            _apiService ??= new ApiService();
            _geofenceService ??= new GeofenceService(_apiService);
        }
    }

    protected override async void OnAppearing()
    {
        try
        {
            base.OnAppearing();

            // Tránh lỗi nén tài nguyên trên Android cũ khi load quá nhanh lúc bật app
            await Task.Delay(200);

            // Load map ngay lập tức với danh sách POI trống
            if (!_isMapLoaded)
            {
                LoadMap();
                _isMapLoaded = true;
            }

            // [ANTI CRASH] Đợi WebView nạp HTML vào bộ nhớ đệm an toàn rồi mới xin quyền GPS (tránh xung đột vòng đời Activity gây văng app)
            await Task.Delay(1000); 

            // Load POI từ API trong background — an toàn, không crash app
            _ = LoadPoisInBackgroundAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"[MapPage] POI load faulted: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);

            // Update offline banner
            UpdateOfflineBanner();

            // Bắt đầu tracking GPS với background support
            try
            {
                await _locationService.StartTrackingWithForegroundAsync();
            }
            catch (Exception gpsEx)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] GPS tracking start error: {gpsEx.Message}");
            }

            // Xử lý pending POI ID nếu có (khi navigate từ tab khác)
            if (_pendingPoiId.HasValue && _isJsMapReady && _pois != null)
            {
                ProcessPendingPoiId();
            }

            // Xử lý pending Tour ID nếu có (vẽ lộ trình)
            if (_pendingTourId.HasValue)
            {
                ProcessPendingTourId();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage.OnAppearing] ERROR: {ex}");
#if DEBUG
            try { await DisplayAlert($"⚠️ {LanguageService.GetString("Error")}", ex.Message, LanguageService.GetString("OK")); }
            catch { }
#endif
        }
    }

    /// <summary>
    /// Load POI từ API mà không block UI và không để VS break.
    /// Nếu API lỗi, map vẫn chạy bình thường (không có markers).
    /// </summary>
    private async Task LoadPoisInBackgroundAsync()
    {
        try
        {
            if (_pois == null || !_pois.Any())
            {
                // Chạy HTTP call trên background thread — tránh VS break trên UI thread
                var pois = await Task.Run(async () =>
                {
                    try { 
                        await ApiService.AutoDiscoverApiAsync();
                        return await _apiService.GetAllPOIsAsync(); 
                    }
                    catch { return new List<POI>(); } // nuốt tất cả lỗi kết nối
                });

                _pois = pois;
                
                // Debug: Log translation data
                if (_pois?.Any() == true)
                {
                    var samplePoi = _pois.FirstOrDefault(p => p.Translations?.Any() == true);
                    if (samplePoi != null)
                    {
                        var trans = samplePoi.Translations.Select(t => $"{t.Language}:{t.Description?[..Math.Min(20, t.Description?.Length ?? 0)]}");
                        System.Diagnostics.Debug.WriteLine($"[MapPage] Loaded POI {samplePoi.Id} with translations: {string.Join(", ", trans)}");
                    }
                    else
                    {
                        var firstPoi = _pois.FirstOrDefault();
                        if (firstPoi != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[MapPage] POI {firstPoi.Id} has {firstPoi.Translations?.Count ?? 0} translations");
                        }
                    }
                }

                // Cập nhật GeofenceService
                _geofenceService.SetPois(_pois);

                // Nếu có POI → refresh markers trên map
                QueueMapMarkerRefresh();
                
                // Xử lý pending POI ID nếu có
                ProcessPendingPoiId();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] LoadPoisInBackground error: {ex.Message}");
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Dừng GPS tracking (với foreground service trên Android)
        _locationService.StopTrackingWithForeground();
        // Dừng audio thuyết minh khi rời khỏi trang
        AudioPlayerService.Instance.Stop();
        // Dừng TTS (Text-to-Speech) nếu đang phát
        _geofenceService.CancelTTS();
        // Unsubscribe from language changes
        LanguageService.LanguageChanged -= OnLanguageChanged;
    }
    
    private void OnConnectivityChanged(bool isOnline)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateOfflineBanner();
            if (isOnline)
            {
                // Reload map + POIs khi có mạng trở lại
                if (!_isJsMapReady)
                {
                    _isMapLoaded = false;
                    LoadMap();
                    _isMapLoaded = true;
                }
                if (_pois == null || !_pois.Any())
                {
                    _ = LoadPoisInBackgroundAsync();
                }
            }
        });
    }

    private void UpdateOfflineBanner()
    {
        OfflineBanner.IsVisible = !NetworkService.IsConnected;
    }

    private void OnWebViewHandlerChanged(object? sender, EventArgs e)
    {
#if ANDROID
        try
        {
            if (MapWebView.Handler?.PlatformView is Android.Webkit.WebView androidWebView)
            {
                var settings = androidWebView.Settings;
                // LOAD_CACHE_ELSE_NETWORK = 1
                settings.CacheMode = (Android.Webkit.CacheModes)1;
                settings.DomStorageEnabled = true;
                settings.DatabaseEnabled = true;
                settings.SetGeolocationEnabled(true);
                settings.AllowFileAccess = true;
                settings.AllowContentAccess = true;
                // Tăng kích thước cache
                settings.SetAppCacheMaxSize(50 * 1024 * 1024); // 50MB
                var cachePath = System.IO.Path.Combine(FileSystem.AppDataDirectory, "webview_cache");
                Directory.CreateDirectory(cachePath);
                settings.SetAppCachePath(cachePath);
                settings.SetAppCacheEnabled(true);
                System.Diagnostics.Debug.WriteLine("[MapPage] Android WebView cache enabled (LOAD_CACHE_ELSE_NETWORK)");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] WebView cache setup error: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Xử lý khi ngôn ngữ thay đổi - refresh mô tả POI nếu bottom sheet đang mở
    /// </summary>
    private void OnLanguageChanged(object? sender, string newLang)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                // Update GeofenceService language
                _geofenceService.CurrentLanguage = newLang;
                
                // Refresh POI description if bottom sheet is visible
                if (_currentPoi != null && BottomSheetView?.IsVisible == true && PoiDescLabel != null)
                {
                    PoiDescLabel.Text = _currentPoi.GetLocalizedDescription(newLang);
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Refreshed POI description for language: {newLang}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] OnLanguageChanged error: {ex.Message}");
            }
        });
    }
    
    /// <summary>
    /// Xử lý POI ID từ navigation (ví dụ: từ HomePage khi click vào quán ăn)
    /// </summary>
    private void ProcessPendingPoiId()
    {
        if (_pendingPoiId.HasValue && _pois != null && _pois.Any())
        {
            var poi = _pois.FirstOrDefault(p => p.Id == _pendingPoiId.Value);
            if (poi != null)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Processing pending POI: {poi.Name}");
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    // Hiển thị chi tiết POI
                    ShowPoiDetails(poi);

                    // Fly đến vị trí POI trên bản đồ và highlight marker
                    if (_isJsMapReady)
                    {
                        MapWebView.Eval($"map.flyTo({{center: [{poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
                        MapWebView.Eval($"highlightPoi({poi.Id});");
                    }

                    // Nếu đến từ QR scan, tự động phát audio
                    if (_triggerFromQR)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MapPage] Auto-playing audio for QR-scanned POI: {poi.Name}");
                        _ = _geofenceService.TriggerFromQRAsync(poi);
                        _triggerFromQR = false; // Reset flag sau khi xử lý
                    }
                });
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Pending POI ID {_pendingPoiId.Value} not found");
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert(LanguageService.GetString("Error"), 
                        LanguageService.GetString("SearchNotFound", _pendingPoiId.Value.ToString()), 
                        LanguageService.GetString("OK"));
                });
            }
            _pendingPoiId = null;
        }
    }

    /// <summary>
    /// Xử lý Tour ID từ navigation (ví dụ: từ TourPage khi click vào tour)
    /// </summary>
    private async void ProcessPendingTourId()
    {
        if (!_pendingTourId.HasValue) return;

        try
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] Processing pending Tour ID: {_pendingTourId.Value}, JS ready={_isJsMapReady}");
            
            var tour = await _apiService.GetTourByIdAsync(_pendingTourId.Value);
            if (tour == null) { _pendingTourId = null; return; }

            var tourPois = await _apiService.GetTourStopsAsync(tour.Id);
            if (tourPois == null || !tourPois.Any()) { _pendingTourId = null; return; }

            var originPOI = tourPois.First().POI;
            var destPOI = tourPois.Last().POI;
            var dest = new Location(destPOI!.Latitude, destPOI.Longitude);
            
            // Get actual user location - try mock, then cached, then GPS (like OnDirectionsClicked)
            var origin = _locationService.MockLocation ?? _lastLocation;
            if (origin == null)
            {
                try
                {
                    var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                    origin = await Geolocation.Default.GetLocationAsync(request);
                    if (origin != null) _lastLocation = origin;
                }
                catch { }
            }
            
            // If still no location, show error and abort
            if (origin == null)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await DisplayAlert("GPS", LanguageService.GetString("GPSError"), LanguageService.GetString("OK"));
                });
                _pendingTourId = null;
                return;
            }
            
            // Build waypoints: first POI + all intermediate POIs
            var waypoints = new List<Location>();
            waypoints.Add(new Location(originPOI!.Latitude, originPOI.Longitude));
            for (int i = 1; i < tourPois.Count - 1; i++)
            {
                waypoints.Add(new Location(tourPois[i].POI!.Latitude, tourPois[i].POI!.Longitude));
            }
            
            // Nếu map JS chưa ready thì giữ lại pending để retry sau
            if (!_isJsMapReady)
            {
                System.Diagnostics.Debug.WriteLine("[MapPage] Map JS not ready yet, will retry when ready");
                return;
            }
            
            // Wait for markers to be added to DOM, then highlight tour POIs
            if (_pois != null && tourPois.Any())
            {
                try
                {
                    var jsArray = string.Join(",", tourPois.Select(tp => tp.POIId.ToString()));
                    // Small delay to ensure markers are in DOM
                    await Task.Delay(300);
                    MainThread.BeginInvokeOnMainThread(() => {
                        MapWebView.Eval($"highlightPoisGreen([{jsArray}]);");
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Highlight POIs error: {ex.Message}");
                }
            }

            // Draw directions route (Goong → OSRM → straight-line fallback)
            bool routeSuccess = false;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Calling DrawRouteAsync: origin={origin.Latitude},{origin.Longitude} dest={dest.Latitude},{dest.Longitude} waypoints={waypoints.Count}");
                await DrawRouteAsync(origin, dest, waypoints);
                routeSuccess = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] DrawRouteAsync error: {ex.Message}");
            }
            
            // If API routing failed, fall back to straight-line route (user → all POIs)
            if (!routeSuccess)
            {
                try
                {
                    var ci = System.Globalization.CultureInfo.InvariantCulture;
                    var allCoords = new List<string>();
                    allCoords.Add($"[{origin.Longitude.ToString(ci)},{origin.Latitude.ToString(ci)}]");
                    foreach (var tp in tourPois)
                    {
                        allCoords.Add($"[{tp.POI!.Longitude.ToString(ci)},{tp.POI.Latitude.ToString(ci)}]");
                    }
                    var coordsStr = string.Join(",", allCoords);
                    MainThread.BeginInvokeOnMainThread(() => {
                        MapWebView.Eval($"drawRoute([{coordsStr}]);");
                    });
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Fallback straight-line route with {allCoords.Count} points");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Straight-line fallback error: {ex.Message}");
                }
            }

            // Re-draw user marker at actual user location
            if (_isJsMapReady)
            {
                var ci = System.Globalization.CultureInfo.InvariantCulture;
                MainThread.BeginInvokeOnMainThread(() => {
                    MapWebView.Eval($"updateUserLocation({origin.Longitude.ToString(ci)}, {origin.Latitude.ToString(ci)});");
                });
            }

            _pendingTourId = null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] ProcessPendingTourId fatal error: {ex.Message}");
            _pendingTourId = null;
        }
    }

    private void OnLocationChanged(object? sender, Location location)
    {
        if (location == null) return;
        
        _lastLocation = location;

        bool shouldCheckGeofence = true;
        if (_lastCheckedLocation != null)
        {
            var movedMeters = Location.CalculateDistance(
                _lastCheckedLocation.Latitude, _lastCheckedLocation.Longitude,
                location.Latitude, location.Longitude,
                DistanceUnits.Kilometers) * 1000;
            shouldCheckGeofence = movedMeters > 5;
        }

        if (shouldCheckGeofence)
        {
            _lastCheckedLocation = location;
            if (_geofenceService != null)
            {
                _ = Task.Run(() => _geofenceService.CheckGeofences(location));
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (_isJsMapReady && MapWebView != null)
                {
                    MapWebView.Eval($"updateUserLocation({location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                }

                if (BottomSheetView?.IsVisible == true && _currentPoi != null && PoiDistanceLabel != null)
                {
                    var dist = Location.CalculateDistance(
                        location.Latitude, location.Longitude,
                        _currentPoi.Latitude, _currentPoi.Longitude,
                        DistanceUnits.Kilometers) * 1000;
                    PoiDistanceLabel.Text = dist < 1000
                        ? $"• {(int)dist}m từ bạn"
                        : $"• {dist / 1000:F1}km từ bạn";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] OnLocationChanged UI error: {ex.Message}");
            }
        });
    }


    private void OnPoiTriggered(object? sender, POI poi)
    {
        System.Diagnostics.Debug.WriteLine($"[MapPage] OnPoiTriggered: {poi.Name} (ID={poi.Id})");
        MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
    }

    private void ShowPoiDetails(POI poi)
    {
        if (poi == null) return;
        
        _currentPoi = poi;
        
        // All UI updates must be on main thread with null checks
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                if (PoiNameLabel != null)
                    PoiNameLabel.Text = poi.Name;
                if (PoiDescLabel != null)
                    PoiDescLabel.Text = poi.GetLocalizedDescription(LanguageService.CurrentLanguage);
                if (PoiRatingLabel != null)
                    PoiRatingLabel.Text = $"⭐ {poi.Rating:F1}";
                if (PoiDistanceLabel != null)
                    PoiDistanceLabel.Text = $"• {poi.Radius}m bán kính";

                if (_lastLocation != null && PoiDistanceLabel != null)
                {
                    var dist = Location.CalculateDistance(
                        _lastLocation.Latitude, _lastLocation.Longitude,
                        poi.Latitude, poi.Longitude,
                        DistanceUnits.Kilometers) * 1000;
                    PoiDistanceLabel.Text = dist < 1000 ? $"• {(int)dist}m từ bạn" : $"• {dist / 1000:F1}km từ bạn";
                }

                if (PoiImage != null)
                {
                    if (!string.IsNullOrEmpty(poi.ImageUrl))
                    {
                        if (File.Exists(poi.ImageUrl))
                            PoiImage.Source = ImageSource.FromFile(poi.ImageUrl);
                        else
                            PoiImage.Source = ImageSource.FromUri(new Uri(poi.ImageUrl));
                    }
                    else
                    {
                        PoiImage.Source = null;
                    }
                }

                bool isFav = Preferences.Default.Get($"fav_{poi.Id}", false);
                if (FavBtn != null)
                    FavBtn.Text = isFav ? "❤️" : "🤍";

                if (BottomSheetView != null)
                    BottomSheetView.IsVisible = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] ShowPoiDetails error: {ex.Message}");
            }
        });
    }

    private void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e == null) return;
        
        System.Diagnostics.Debug.WriteLine($"[MapWebView Navigation] URL: {e.Url}");

        if (e.Url?.StartsWith("http://map/ready", StringComparison.OrdinalIgnoreCase) == true)
        {
            e.Cancel = true;
            _isJsMapReady = true;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    TryRefreshMapMarkers();
                    // Process pending POI if POIs are already loaded
                    ProcessPendingPoiId();
                    // Process pending Tour route when map JS is ready
                    ProcessPendingTourId();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Map ready error: {ex.Message}");
                }
            });
            // Cache map assets (JS/CSS/Style) cho lần offline sau
            _ = CacheMapAssetsAsync();
            return;
        }

        if (e.Url.StartsWith("poi://selected") || e.Url.StartsWith("http://poi/selected"))
        {
            e.Cancel = true;
            try
            {
                var uri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (int.TryParse(query["id"], out int poiId))
                {
                    var poi = _pois?.FirstOrDefault(p => p.Id == poiId);
                    if (poi != null) MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapWebView] Error: {ex.Message}");
            }
            return;
        }

        if (e.Url.StartsWith("http://map/setmock", StringComparison.OrdinalIgnoreCase))
        {
            e.Cancel = true;
            if (!_locationService.IsMocking) return;
            try
            {
                var uri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (double.TryParse(query["lat"], System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
                    double.TryParse(query["lng"], System.Globalization.CultureInfo.InvariantCulture, out double lng))
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Set Mock GPS to {lat}, {lng}");
                    var mockLoc = new Location(lat, lng);
                    _locationService.MockLocation = mockLoc;
                    _lastLocation = mockLoc;
                    // Gửi mock location đến API ngay lập tức → heatmap CMS cập nhật real-time
                    _ = Task.Run(async () => await _locationService.SendMockLocationNowAsync());
                    MainThread.BeginInvokeOnMainThread(() => {
                        if (_isJsMapReady)
                        {
                            MapWebView.Eval($"updateUserLocation({lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
                            MapWebView.Eval($"map.flyTo({{center: [{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
                        }
                        // Trigger geofence check immediately for mock location
                        System.Diagnostics.Debug.WriteLine($"[MapPage] Mock set: POIs loaded={_pois?.Count ?? 0}, geofenceService={_geofenceService != null}");
                        if (_pois != null && _pois.Any())
                        {
                            foreach (var p in _pois)
                                System.Diagnostics.Debug.WriteLine($"[MapPage] POI #{p.Id}: {p.Name} lat={p.Latitude:F6} lng={p.Longitude:F6} active={p.IsActive} radius={p.Radius}m");
                            var nearby = _pois
                                .Where(p => p.IsActive)
                                .Select(p => new { Poi = p, Dist = Location.CalculateDistance(mockLoc.Latitude, mockLoc.Longitude, p.Latitude, p.Longitude, DistanceUnits.Kilometers) * 1000 })
                                .Where(x => x.Dist <= Math.Max(x.Poi.Radius, 50))
                                .OrderBy(x => x.Dist)
                                .ToList();
                            var inactive = _pois.Where(p => !p.IsActive).ToList();
                            System.Diagnostics.Debug.WriteLine($"[MapPage] Mock geofence: {nearby.Count} POIs in range, {inactive.Count} inactive POIs");
                            foreach (var n in nearby)
                                System.Diagnostics.Debug.WriteLine($"  -> {n.Poi.Name}: {n.Dist:F0}m (radius={Math.Max(n.Poi.Radius, 50)}m)");
                            _geofenceService.CheckGeofences(mockLoc);
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[MapPage] Mock geofence: POIs not loaded yet, cannot check");
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapWebView] Mock GPS Error: {ex.Message}");
            }
        }
    }

    private void OnPlayAudioClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        // Dùng ScriptText từ DB (đúng ngôn ngữ CurrentLanguage), fallback về Description
        _ = _geofenceService.SpeakNarrationAsync(_currentPoi);
    }

    private async void OnMockToggleClicked(object? sender, EventArgs e)
    {
        _locationService.IsMocking = !_locationService.IsMocking;
        MockBanner.IsVisible = _locationService.IsMocking;
        MockToggleIcon.TextColor = _locationService.IsMocking ? Color.Parse("#FF6B00") : Color.Parse("#4A5568");
        System.Diagnostics.Debug.WriteLine($"[MapPage] Mock mode: {_locationService.IsMocking}");

        // Toggle JS click handler for mock location (mobile touch support)
        if (_isJsMapReady)
        {
            MapWebView.Eval($"toggleMockClick({_locationService.IsMocking.ToString().ToLower()});");
        }

// When turning off mock mode, clear mock location and restore real GPS
if (!_locationService.IsMocking)
{
    _locationService.MockLocation = null;

    try
    {
        var request = new GeolocationRequest(
            GeolocationAccuracy.Medium,
            TimeSpan.FromSeconds(8));

        var realLocation = await Geolocation.Default.GetLocationAsync(request);

        if (realLocation != null)
        {
            _lastLocation = realLocation;

            if (_isJsMapReady)
            {
                var lng = realLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture);
                var lat = realLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture);

                MapWebView.Eval($"updateUserLocation({lng}, {lat})");
                MapWebView.Eval($"map.flyTo({{center: [{lng}, {lat}], zoom: 16}})");
            }

            System.Diagnostics.Debug.WriteLine(
                $"[MapPage] Restored real GPS: {realLocation.Latitude}, {realLocation.Longitude}");
        }

        _ = Task.Run(async () => await _locationService.SendRealLocationNowAsync());
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[MapPage] Failed to restore real GPS: {ex.Message}");
    }
}
    }

    private void OnZoomInClicked(object? sender, EventArgs e)
    {
        if (_isJsMapReady)
            MapWebView.Eval("map.zoomIn();");
    }

    private void OnZoomOutClicked(object? sender, EventArgs e)
    {
        if (_isJsMapReady)
            MapWebView.Eval("map.zoomOut();");
    }

    private async void OnDirectionsClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        
        var origin = _locationService.MockLocation ?? _lastLocation;
        if (origin == null)
        {
            // Thử lấy vị trí hiện tại một lần nữa
            await DisplayAlert("GPS", LanguageService.GetString("Locating"), LanguageService.GetString("OK"));
            try
            {
                var request = new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10));
                origin = await Geolocation.Default.GetLocationAsync(request);
                if (origin != null)
                {
                    _lastLocation = origin;
                }
            }
            catch { }
            
            if (origin == null)
            {
                await DisplayAlert("GPS", LanguageService.GetString("GPSError"), LanguageService.GetString("OK"));
                return;
            }
        }

        BottomSheetView.IsVisible = false;

        // Highlight selected POI green + blink (same style as tour)
        if (_isJsMapReady)
        {
            MapWebView.Eval($"highlightPoiGreen({_currentPoi.Id});");
        }

        // Draw route and ensure user marker stays visible
        var routeCoords = await DrawRouteAsync(origin, new Location(_currentPoi.Latitude, _currentPoi.Longitude));

        // Save popular route for CMS analytics (fire-and-forget)
        if (routeCoords != null && routeCoords.Count >= 2)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var coords = routeCoords.Select(l => new[] { l.Longitude, l.Latitude }).ToList();
                    var deviceId = ApiService.GetStableDeviceId();
                    await _apiService.SaveRouteAsync(coords, deviceId);
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Popular route saved: {coords.Count} points");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MapPage] Save route error: {ex.Message}");
                }
            });
        }

        // Re-draw user marker after route to ensure visibility
        if (_isJsMapReady && origin != null)
        {
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            MapWebView.Eval($"updateUserLocation({origin.Longitude.ToString(ci)}, {origin.Latitude.ToString(ci)});");
        }
    }

    private async Task<List<Location>> DrawRouteAsync(Location origin, Location destination, List<Location>? waypoints = null)
    {
        var allInputPoints = new List<Location> { origin };
        if (waypoints != null) allInputPoints.AddRange(waypoints);
        allInputPoints.Add(destination);
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        // Try Goong Directions API first (best for Vietnam roads)
        if (!string.IsNullOrEmpty(GoongRestApiKey))
        {
            try
            {
                var goongResult = await TryGoongDirections(allInputPoints, ci);
                if (goongResult != null) return goongResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DrawRoute] Goong failed: {ex.Message}");
            }
        }

        // Fallback to OSRM
        try
        {
            var osrmResult = await TryOsrmDirections(allInputPoints, ci);
            if (osrmResult != null) return osrmResult;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DrawRoute] OSRM failed: {ex.Message}");
        }

        // Both APIs failed
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("DirectionsAPIError"), LanguageService.GetString("OK"));
        });
        throw new Exception("All routing APIs failed");
    }

    /// <summary>
    /// Goong Directions API - xử lý waypoints như Google Maps (mỗi POI là đích thực sự)
    /// </summary>
    private async Task<List<Location>?> TryGoongDirections(List<Location> allPoints, System.Globalization.CultureInfo ci)
    {
        var allPolyCoords = new List<Location>();
        var dashedSegments = new List<string>();

        // Process each leg separately: Origin->WP1, WP1->WP2, ..., WPn->Destination
        // This ensures each POI is treated as a real destination, not a via point
        for (int i = 0; i < allPoints.Count - 1; i++)
        {
            var start = allPoints[i];
            var end = allPoints[i + 1];

            var url = $"https://rsapi.goong.io/Direction?origin={start.Latitude.ToString(ci)},{start.Longitude.ToString(ci)}" +
                      $"&destination={end.Latitude.ToString(ci)},{end.Longitude.ToString(ci)}" +
                      $"&vehicle=car&api_key={GoongRestApiKey}";

            System.Diagnostics.Debug.WriteLine($"[DrawRoute] Goong leg {i}: {start.Latitude:F6},{start.Longitude:F6} -> {end.Latitude:F6},{end.Longitude:F6}");

            var res = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(res);

            if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
                return null;

            var route = routes[0];
            if (!route.TryGetProperty("overview_polyline", out var overviewPl)) return null;
            var encoded = overviewPl.GetProperty("points").GetString();
            if (string.IsNullOrEmpty(encoded)) return null;

            var legPolyCoords = DecodePolyline(encoded);
            if (legPolyCoords.Count < 2) continue;

            // For origin of first leg: check if need dashed line from polyline start to actual origin
            if (i == 0)
            {
                var polyStart = legPolyCoords.First();
                var dist = Location.CalculateDistance(polyStart, start, DistanceUnits.Kilometers) * 1000;
                if (dist > 5)
                    dashedSegments.Add($"[[{polyStart.Longitude.ToString(ci)},{polyStart.Latitude.ToString(ci)}],[{start.Longitude.ToString(ci)},{start.Latitude.ToString(ci)}]]");
            }

            // For destination of each leg (intermediate POI or final dest): check if need dashed
            var polyEnd = legPolyCoords.Last();
            var endDist = Location.CalculateDistance(polyEnd, end, DistanceUnits.Kilometers) * 1000;
            if (endDist > 5)
                dashedSegments.Add($"[[{polyEnd.Longitude.ToString(ci)},{polyEnd.Latitude.ToString(ci)}],[{end.Longitude.ToString(ci)},{end.Latitude.ToString(ci)}]]");

            // Add this leg's coords to total (avoid duplicating the connection point)
            if (allPolyCoords.Count > 0 && allPolyCoords.Last().Latitude == legPolyCoords.First().Latitude 
                && allPolyCoords.Last().Longitude == legPolyCoords.First().Longitude)
            {
                // Skip first point if same as last point of previous leg
                allPolyCoords.AddRange(legPolyCoords.Skip(1));
            }
            else
            {
                allPolyCoords.AddRange(legPolyCoords);
            }
        }

        if (allPolyCoords.Count < 2) return null;

        var jsCoords = string.Join(",", allPolyCoords.Select(c => $"[{c.Longitude.ToString(ci)},{c.Latitude.ToString(ci)}]"));
        if (_isJsMapReady)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapWebView.Eval($"drawRoute([{jsCoords}]);");
                if (dashedSegments.Count > 0)
                    MapWebView.Eval($"drawDashedRoute([{string.Join(",", dashedSegments)}]);");
            });
        }
        System.Diagnostics.Debug.WriteLine($"[DrawRoute] Goong success: {allPolyCoords.Count} total points, {dashedSegments.Count} dashed");
        return allPolyCoords;
    }

    /// <summary>
    /// OSRM fallback - xử lý waypoints như Google Maps (mỗi POI là đích thực sự)
    /// </summary>
    private async Task<List<Location>?> TryOsrmDirections(List<Location> allPoints, System.Globalization.CultureInfo ci)
    {
        var allPolyCoords = new List<Location>();
        var dashedSegments = new List<string>();

        // Process each leg separately like Goong version
        for (int i = 0; i < allPoints.Count - 1; i++)
        {
            var start = allPoints[i];
            var end = allPoints[i + 1];

            var url = $"https://router.project-osrm.org/route/v1/driving/" +
                      $"{start.Longitude.ToString(ci)},{start.Latitude.ToString(ci)};" +
                      $"{end.Longitude.ToString(ci)},{end.Latitude.ToString(ci)}" +
                      $"?overview=full&geometries=polyline";

            System.Diagnostics.Debug.WriteLine($"[DrawRoute] OSRM leg {i}: {start.Latitude:F6},{start.Longitude:F6} -> {end.Latitude:F6},{end.Longitude:F6}");

            var res = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(res);
            if (!doc.RootElement.TryGetProperty("routes", out var routes) || routes.GetArrayLength() == 0)
                return null;

            var geometry = routes[0].GetProperty("geometry").GetString();
            if (string.IsNullOrEmpty(geometry)) continue;

            var legPolyCoords = DecodePolyline(geometry);
            if (legPolyCoords.Count < 2) continue;

            // Check origin of first leg
            if (i == 0)
            {
                var polyStart = legPolyCoords.First();
                var dist = Location.CalculateDistance(polyStart, start, DistanceUnits.Kilometers) * 1000;
                if (dist > 5)
                    dashedSegments.Add($"[[{polyStart.Longitude.ToString(ci)},{polyStart.Latitude.ToString(ci)}],[{start.Longitude.ToString(ci)},{start.Latitude.ToString(ci)}]]");
            }

            // Check destination of each leg
            var polyEnd = legPolyCoords.Last();
            var endDist = Location.CalculateDistance(polyEnd, end, DistanceUnits.Kilometers) * 1000;
            if (endDist > 5)
                dashedSegments.Add($"[[{polyEnd.Longitude.ToString(ci)},{polyEnd.Latitude.ToString(ci)}],[{end.Longitude.ToString(ci)},{end.Latitude.ToString(ci)}]]");

            // Merge coords (avoid duplicates)
            if (allPolyCoords.Count > 0 && allPolyCoords.Last().Latitude == legPolyCoords.First().Latitude 
                && allPolyCoords.Last().Longitude == legPolyCoords.First().Longitude)
            {
                allPolyCoords.AddRange(legPolyCoords.Skip(1));
            }
            else
            {
                allPolyCoords.AddRange(legPolyCoords);
            }
        }

        if (allPolyCoords.Count < 2) return null;

        var jsCoords = string.Join(",", allPolyCoords.Select(c => $"[{c.Longitude.ToString(ci)},{c.Latitude.ToString(ci)}]"));
        if (_isJsMapReady)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                MapWebView.Eval($"drawRoute([{jsCoords}]);");
                if (dashedSegments.Count > 0)
                    MapWebView.Eval($"drawDashedRoute([{string.Join(",", dashedSegments)}]);");
            });
        }
        System.Diagnostics.Debug.WriteLine($"[DrawRoute] OSRM success: {allPolyCoords.Count} total points, {dashedSegments.Count} dashed");
        return allPolyCoords;
    }

    /// <summary>
    /// Build dashed line segments from nearest route point to each POI (if POI > 20m from route)
    /// </summary>
    private static List<string> BuildDashedSegments(List<Location> inputPoints, List<Location> routePoints, System.Globalization.CultureInfo ci, double thresholdMeters = 5)
    {
        var segments = new List<string>();
        if (routePoints.Count == 0) return segments;

        foreach (var pt in inputPoints)
        {
            double minDist = double.MaxValue;
            Location? nearest = null;
            foreach (var rp in routePoints)
            {
                var d = Location.CalculateDistance(pt.Latitude, pt.Longitude, rp.Latitude, rp.Longitude, DistanceUnits.Kilometers) * 1000;
                if (d < minDist) { minDist = d; nearest = rp; }
            }

            if (nearest != null && minDist > thresholdMeters)
            {
                segments.Add($"[[{nearest.Longitude.ToString(ci)},{nearest.Latitude.ToString(ci)}],[{pt.Longitude.ToString(ci)},{pt.Latitude.ToString(ci)}]]");
            }
        }
        return segments;
    }

    private static List<Location> DecodePolyline(string encoded)
    {
        var polyline = new List<Location>();
        if (string.IsNullOrEmpty(encoded)) return polyline;
        int index = 0, len = encoded.Length, lat = 0, lng = 0;
        while (index < len)
        {
            int b, shift = 0, result = 0;
            do { b = encoded[index++] - 63; result |= (b & 0x1f) << shift; shift += 5; } while (b >= 0x20);
            lat += ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            shift = 0; result = 0;
            do { b = encoded[index++] - 63; result |= (b & 0x1f) << shift; shift += 5; } while (b >= 0x20);
            lng += ((result & 1) != 0 ? ~(result >> 1) : (result >> 1));
            polyline.Add(new Location((double)lat / 1E5, (double)lng / 1E5));
        }
        return polyline;
    }

    private void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        bool isFav = Preferences.Default.Get($"fav_{_currentPoi.Id}", false);
        Preferences.Default.Set($"fav_{_currentPoi.Id}", !isFav);
        FavBtn.Text = !isFav ? "❤️" : "🤍";
    }

    private void OnCloseSheetClicked(object? sender, EventArgs e)
    {
        try
        {
            if (BottomSheetView != null)
                BottomSheetView.IsVisible = false;
            
            // Dừng audio thuyết minh khi đóng chi tiết quán
            AudioPlayerService.Instance.Stop();
            
            // Dừng TTS (Text-to-Speech) nếu đang phát
            _geofenceService?.CancelTTS();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] OnCloseSheetClicked error: {ex.Message}");
        }
    }

    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);
        AudioPlayerService.Instance.PropertyChanged += OnAudioPlayerPropertyChanged;
        AudioPlayerService.Instance.QueueChanged += OnAudioQueueChanged;
        UpdateAudioPlayerUI();
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        AudioPlayerService.Instance.PropertyChanged -= OnAudioPlayerPropertyChanged;
        AudioPlayerService.Instance.QueueChanged -= OnAudioQueueChanged;
    }

    private void OnAudioQueueChanged(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdateAudioPlayerUI);
    }

    private void OnAudioPlayerPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(UpdateAudioPlayerUI);
    }

    private void UpdateAudioPlayerUI()
    {
        try
        {
            var svc = AudioPlayerService.Instance;
            if (svc == null) return;
            
            if (FloatingAudioPlayer == null) return;
            
            if (svc.IsPlaying || !string.IsNullOrEmpty(svc.CurrentAudioTitle) || svc.HasItemsInQueue)
            {
                FloatingAudioPlayer.IsVisible = true;
                if (AudioTitleLabel != null)
                    AudioTitleLabel.Text = svc.CurrentAudioTitle;
                if (AudioPlayPauseBtn != null)
                    AudioPlayPauseBtn.Text = svc.IsPlaying ? "⏸" : "▶";
                
                // Update queue count
                if (svc.QueueCount > 0 && AudioQueueCountLabel != null)
                {
                    AudioQueueCountLabel.Text = $"+{svc.QueueCount} trong hàng đợi";
                    AudioQueueCountLabel.IsVisible = true;
                }
                else if (AudioQueueCountLabel != null)
                {
                    AudioQueueCountLabel.IsVisible = false;
                }
            }
            else
            {
                FloatingAudioPlayer.IsVisible = false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] UpdateAudioPlayerUI error: {ex.Message}");
        }
    }

    private void OnToggleAudioClicked(object? sender, EventArgs e)
    {
        AudioPlayerService.Instance.TogglePlayPause();
    }

    private void OnStopAudioClicked(object? sender, EventArgs e)
    {
        AudioPlayerService.Instance.StopAndClear();
    }

    private void OnSkipAudioClicked(object? sender, EventArgs e)
    {
        AudioPlayerService.Instance.SkipCurrent();
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
        var results = _pois?
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

    private void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is POI selectedPoi)
        {
            SearchDropdown.IsVisible = false;
            SearchEntry.Text = selectedPoi.Name;
            SearchResultsView.SelectedItem = null;

            // Fly to POI and show details
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            if (_isJsMapReady)
                MapWebView.Eval($"map.flyTo({{center: [{selectedPoi.Longitude.ToString(ci)}, {selectedPoi.Latitude.ToString(ci)}], zoom: 17}});");
            ShowPoiDetails(selectedPoi);
        }
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        SearchDropdown.IsVisible = false;
        await DoSearch(SearchEntry.Text);
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
        SearchDropdown.IsVisible = false;
        await DoSearch(SearchEntry.Text);
    }

    // ⚙️ Nút đổi IP server (không cần rebuild app)
    private async void OnSettingsClicked(object? sender, EventArgs e)
    {
        var current = ApiService.BaseUrl;
        var result = await DisplayPromptAsync(
            "⚙️ Cài đặt Server",
            "Nhập địa chỉ API của máy tính\n(VD: http://192.168.1.7:5254)",
            initialValue: current,
            placeholder: "http://IP:PORT",
            keyboard: Keyboard.Url);

        if (string.IsNullOrWhiteSpace(result)) return;

        result = result.TrimEnd('/');
        if (!result.StartsWith("http"))
        {
            await DisplayAlert(LanguageService.GetString("Error"), LanguageService.GetString("AddressRequired"), LanguageService.GetString("OK"));
            return;
        }

        ApiService.UpdateBaseUrl(result);

        // Test kết nối
        var ok = await new ApiService().TestConnectionAsync();
        if (ok)
        {
            // Reload POI với URL mới
            _pois = null;
            _isMapLoaded = false;
            _pois = await new ApiService().GetAllPOIsAsync();
            LoadMap();
            _isMapLoaded = true;
            await DisplayAlert($"✅ {LanguageService.GetString("ConnectedSuccess")}", 
                LanguageService.GetString("ConnectedLocations", _pois?.Count ?? 0), 
                LanguageService.GetString("OK"));
        }
        else
        {
            await DisplayAlert($"❌ {LanguageService.GetString("ConnectionFailed")}",
                LanguageService.GetString("ConnectionCheck"),
                LanguageService.GetString("OK"));
        }
    }

    private async Task DoSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        // 1. Tìm trong dữ liệu POI local trước
        var local = _pois?.FirstOrDefault(p =>
            p.Name.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (local != null)
        {
            ShowPoiDetails(local);
            MapWebView.Eval($"map.flyTo({{center: [{local.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {local.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
            return;
        }

        // 2. Goong Places API (chỉ gọi nếu có REST API key)
        if (string.IsNullOrEmpty(GoongRestApiKey))
        {
            await DisplayAlert(LanguageService.GetString("Search"), 
                LanguageService.GetString("SearchNotFound", query), 
                LanguageService.GetString("OK"));
            return;
        }

        try
        {
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://rsapi.goong.io/Place/AutoComplete?api_key={GoongRestApiKey}&input={encoded}&location=10.759,106.701";
            var res = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(res);
            var preds = doc.RootElement.GetProperty("predictions");
            if (preds.GetArrayLength() > 0)
            {
                var first = preds[0];
                var name = first.GetProperty("description").GetString();
                var placeId = first.GetProperty("place_id").GetString();
                var detailUrl = $"https://rsapi.goong.io/Place/Detail?api_key={GoongRestApiKey}&place_id={placeId}";
                var detailRes = await _http.GetStringAsync(detailUrl);
                using var detailDoc = JsonDocument.Parse(detailRes);
                var loc = detailDoc.RootElement.GetProperty("result").GetProperty("geometry").GetProperty("location");
                double lat = loc.GetProperty("lat").GetDouble();
                double lng = loc.GetProperty("lng").GetDouble();
                MapWebView.Eval($"map.flyTo({{center: [{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");

                PoiNameLabel.Text = name ?? query;
                PoiDescLabel.Text = "Kết quả từ Goong Maps";
                PoiRatingLabel.Text = "⭐ --";
                PoiDistanceLabel.Text = "";
                PoiImage.Source = null;
                BottomSheetView.IsVisible = true;
            }
        }
        catch (HttpRequestException ex) when ((int?)ex.StatusCode == 403)
        {
            System.Diagnostics.Debug.WriteLine("[Search] Goong 403: REST API Key không hợp lệ hoặc quota hết.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Search] Error: {ex.Message}");
        }
    }

    private void OnRecenterClicked(object? sender, EventArgs e)
    {
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (_lastLocation != null && _isJsMapReady)
        {
            MapWebView.Eval($"updateUserLocation({_lastLocation.Longitude.ToString(ci)}, {_lastLocation.Latitude.ToString(ci)});");
            MapWebView.Eval($"map.flyTo({{center: [{_lastLocation.Longitude.ToString(ci)}, {_lastLocation.Latitude.ToString(ci)}], zoom: 17}});");
        }
        else if (_isJsMapReady)
        {
            MapWebView.Eval("map.flyTo({center: [106.7018, 10.7596], zoom: 17});");
        }
    }

    /// <summary>
    /// QR Scanner: Điều hướng đến QRScannerPage để quét mã thực tế
    /// </summary>
    private async void OnQRScanClicked(object? sender, EventArgs e)
    {
        // Navigate to real QR scanner page
        await Shell.Current.GoToAsync("QRScannerPage");
    }

    private void LoadMap()
    {
        _isJsMapReady = false;
        _pendingMapPoisJson = SerializeMapPois(_pois);
        var poisJson = _pendingMapPoisJson;

        // Offline mode: dùng base64 data URI cho JS/CSS/Style (tránh file:// bị chặn trên device thật)
        var useOffline = !NetworkService.IsConnected && AreMapAssetsCached();
        string? jsBase64 = null, cssBase64 = null, styleBase64 = null;
        if (useOffline)
        {
            try
            {
                jsBase64 = Convert.ToBase64String(File.ReadAllBytes(JsCachePath));
                cssBase64 = Convert.ToBase64String(File.ReadAllBytes(CssCachePath));
                styleBase64 = Convert.ToBase64String(File.ReadAllBytes(StyleCachePath));
                System.Diagnostics.Debug.WriteLine("[MapPage] Offline map: base64 data URI for JS/CSS/Style");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPage] Read offline assets error: {ex.Message}");
                useOffline = false;
            }
        }

        var html = BuildMapHtml(poisJson, useOffline, jsBase64, cssBase64, styleBase64);
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private static string SerializeMapPois(IEnumerable<POI>? pois)
    {
        var mapPois = (pois ?? Enumerable.Empty<POI>())
            .Select(p => new POIMapDto
            {
                Id = p.Id,
                Name = p.Name,
                Latitude = p.Latitude,
                Longitude = p.Longitude
            });

        return System.Text.Json.JsonSerializer.Serialize(mapPois);
    }

    private void QueueMapMarkerRefresh()
    {
        _pendingMapPoisJson = SerializeMapPois(_pois);
        TryRefreshMapMarkers();
    }

    private void TryRefreshMapMarkers()
    {
        if (!_isJsMapReady || string.IsNullOrWhiteSpace(_pendingMapPoisJson) || MapWebView == null)
            return;

        var poisJson = _pendingMapPoisJson;
        _pendingMapPoisJson = null;

        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (MapWebView != null && _isJsMapReady)
                {
                    MapWebView.Eval($"refreshMarkers({poisJson});");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] TryRefreshMapMarkers error: {ex.Message}");
        }
    }

    private static string BuildMapHtml(string poisJson, bool useOfflineAssets = false, string? jsBase64 = null, string? cssBase64 = null, string? styleBase64 = null)
    {
        // JS tag: data URI base64 khi offline (tránh </script> và file:// bị chặn), CDN khi online
        var jsTag = useOfflineAssets && !string.IsNullOrEmpty(jsBase64)
            ? $"<script src='data:text/javascript;base64,{jsBase64}'></script>\n<script>setTimeout(function(){{ if(window.goongjs) onGoongLoaded(); else onGoongLoadError(); }}, 50);</script>"
            : $"<script src='{GoongJsUrl}' onload='onGoongLoaded()' onerror='onGoongLoadError()'></script>";

        // CSS tag: data URI base64 khi offline, CDN khi online
        var cssTag = useOfflineAssets && !string.IsNullOrEmpty(cssBase64)
            ? $"<link href='data:text/css;base64,{cssBase64}' rel='stylesheet'>"
            : $"<link href='{GoongCssUrl}' rel='stylesheet'>";

        // Style JSON: data URI khi offline (goongjs.Map dùng fetch() → data URI hoạt động), null khi online
        var styleObj = !string.IsNullOrEmpty(styleBase64)
            ? "'data:application/json;base64," + styleBase64 + "'"
            : "null";

        return $@"<!DOCTYPE html>
<html>
<head>
<meta name='viewport' content='width=device-width, initial-scale=1.0, maximum-scale=1.0'>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  body, html {{ width:100%; height:100%; overflow:hidden; background:#f0f2f5; }}
  #map {{ width:100%; height:100%; }}

  /* Loading overlay */
  #loading {{
    position:fixed; inset:0; background:#fff;
    display:flex; flex-direction:column;
    align-items:center; justify-content:center;
    z-index:9999; transition:opacity 0.5s;
  }}
  #loading.hide {{ opacity:0; pointer-events:none; }}
  .spinner {{
    width:48px; height:48px; border:5px solid #E0E7FF;
    border-top-color:#4F46E5; border-radius:50%;
    animation:spin 0.8s linear infinite; margin-bottom:16px;
  }}
  @keyframes spin {{ to {{ transform:rotate(360deg); }} }}
  #loading p {{ color:#64748B; font-size:14px; font-family:sans-serif; }}
  #loading .sub {{ color:#94A3B8; font-size:12px; margin-top:4px; }}

  /* Error box */
  #errmsg {{
    display:none; position:fixed; inset:0;
    background:#fff8f0; flex-direction:column;
    align-items:center; justify-content:center; padding:30px;
  }}
  #errmsg.show {{ display:flex; }}
  #errmsg h3 {{ color:#DC2626; font-size:18px; margin-bottom:8px; font-family:sans-serif; }}
  #errmsg p {{ color:#64748B; font-size:13px; text-align:center; font-family:sans-serif; line-height:1.6; }}

  /* POI Markers */
  .poi-marker {{ 
    width:40px; height:40px; border-radius:50%; background:#4F46E5;
    border:3px solid white; cursor:pointer; display:flex; align-items:center;
    justify-content:center; font-size:18px; box-shadow:0 2px 8px rgba(0,0,0,0.3);
    transition:transform 0.2s, background-color 0.2s; user-select:none;
    -webkit-user-select:none; -webkit-touch-callout:none;
  }}
  .poi-marker:hover {{ transform:scale(1.2); background:#6366F1; }}
  .poi-marker:active {{ transform:scale(0.95); }}
  .poi-marker.triggered {{ background:#16A34A; animation:pulseGreen 1s infinite; }}
  @keyframes pulseGreen {{
    0%,100% {{ box-shadow:0 0 0 0 rgba(22,163,74,0.5); }}
    50% {{ box-shadow:0 0 0 12px rgba(22,163,74,0); }}
  }}
  #user-marker {{
    width:20px; height:20px; border-radius:50%; background:#3B82F6;
    border:3px solid white; box-shadow:0 0 0 6px rgba(59,130,246,0.25);
  }}
</style>
</head>
<body>

<!-- Loading overlay -->
<div id='loading'>
  <div class='spinner'></div>
  <p>🗺️ Đang tải bản đồ...</p>
  <p class='sub' id='loading-sub'>Đang kết nối...</p>
</div>

<!-- Error fallback (chỉ hiện khi thực sự không có cache) -->
<div id='errmsg'>
  <h3>⚠️ Không thể tải bản đồ</h3>
  <p>Hãy mở app khi có mạng một lần để cache bản đồ.<br>Sau đó bản đồ sẽ hoạt động offline.</p>
  <button onclick='location.reload()' style='margin-top:16px;padding:10px 24px;background:#00B14F;color:white;border:none;border-radius:12px;font-size:14px;cursor:pointer;'>🔄 Thử lại</button>
</div>

<div id='map'></div>

<!-- QUAN TRỌNG: Khai báo hàm TRƯỚC khi load CDN
     Lý do: onerror/onload của script CDN gọi các hàm này ngay khi CDN load xong.
     Nếu khai báo sau, hàm chưa tồn tại → ReferenceError → map trắng -->
<script>
var map, pois = {poisJson}, userMarker = null, poiMarkers = [];

var STYLE_URL = 'https://tiles.goong.io/assets/goong_map_web.json';
var OFFLINE_STYLE = {styleObj};
var _mapInitialized = false;

function onGoongLoadError() {{
  clearTimeout(window.goongLoadTimeout);
  // CDN không load được — nếu goongjs đã có trong WebView cache thì vẫn dùng được
  if (window.goongjs) {{
    console.log('[Map] CDN error nhưng goongjs có trong cache, init map...');
    initMapWithFallback();
    return;
  }}
  // Thực sự không có goong-js → hiện lỗi
  document.getElementById('loading').style.display = 'none';
  document.getElementById('errmsg').classList.add('show');
}}

function onGoongLoaded() {{
  clearTimeout(window.goongLoadTimeout);
  console.log('[Map] Goong JS loaded OK');
  initMapWithFallback();
}}

function initMapWithFallback() {{
  if (_mapInitialized) return;
  _mapInitialized = true;
  try {{
    goongjs.accessToken = '{GoongMaptileKey}';
    
    // Dùng cached style khi offline (được embed từ C# file cache)
    var styleToUse = OFFLINE_STYLE || STYLE_URL;
    if (OFFLINE_STYLE) {{
      console.log('[Map] Using cached style JSON (offline)');
      document.getElementById('loading-sub').textContent = '📡 Chế độ offline — dùng cache';
    }}
    
    map = new goongjs.Map({{
      container: 'map',
      style: styleToUse,
      center: [106.7018, 10.7596],
      zoom: 16
    }});
    
    map.on('load', function() {{
      document.getElementById('loading').classList.add('hide');
      addMarkers(pois);
      // Style JSON được cache bởi C# (CacheMapAssetsAsync) — không cần localStorage
      setTimeout(function() {{
        window.location.href = 'http://map/ready';
      }}, 0);
    }});
    
    map.on('error', function(e) {{
      console.warn('[Map] Map error:', e.error);
      // Ẩn loading ngay cả khi tile lỗi — map vẫn hiện
      document.getElementById('loading').classList.add('hide');
    }});
    
    // Nếu map load quá lâu (tile lỗi offline), vẫn ẩn loading sau 5s
    setTimeout(function() {{
      var loadEl = document.getElementById('loading');
      if (loadEl && !loadEl.classList.contains('hide')) {{
        loadEl.classList.add('hide');
        addMarkers(pois);
        window.location.href = 'http://map/ready';
      }}
    }}, 5000);
    
    // Mock location: right-click on desktop, click on mobile touch
    map.on('contextmenu', function(e) {{
      window.location.href = 'http://map/setmock?lat=' + e.lngLat.lat + '&lng=' + e.lngLat.lng;
    }});
    // Click handler for mobile (only active when mock mode enabled via toggleMockClick)
    window._mockClickEnabled = false;
    map.on('click', function(e) {{
      if (window._mockClickEnabled) {{
        window.location.href = 'http://map/setmock?lat=' + e.lngLat.lat + '&lng=' + e.lngLat.lng;
      }}
    }});
    window.toggleMockClick = function(enabled) {{
      window._mockClickEnabled = enabled;
    }};
  }} catch(e) {{
    console.error('[Map] initMap error:', e);
    document.getElementById('loading').style.display = 'none';
    document.getElementById('errmsg').classList.add('show');
  }}
}}

function addMarkers(poiList) {{
  if (!map) return;
  poiMarkers.forEach(function(marker) {{ marker.remove(); }});
  poiMarkers = [];

  poiList.forEach(function(poi) {{
    var latitude = poi.Latitude;
    if (latitude === undefined || latitude === null) latitude = poi.latitude;
    var longitude = poi.Longitude;
    if (longitude === undefined || longitude === null) longitude = poi.longitude;
    var lat = Number(latitude);
    var lng = Number(longitude);

    if (!isFinite(lat) || !isFinite(lng)) {{
      console.log('[Marker Skipped] Invalid coordinates');
      return;
    }}

    // JSON from C# uses lowercase property names due to [JsonPropertyName(""id"")]
    var poiId = poi.id || poi.poiId || poi.PoiId || poi.Id || 0;
    var poiName = poi.name || poi.PoiName || poi.Name || 'Địa điểm';

    // Create marker element
    var el = document.createElement('div');
    el.className = 'poi-marker';
    el.setAttribute('data-poi-id', poiId);
    el.setAttribute('style', 'cursor: pointer; pointer-events: auto;');
    el.innerHTML = '🍽️';

    // Create marker with Goong
    var marker = new goongjs.Marker(el)
      .setLngLat([lng, lat])
      .addTo(map);
    poiMarkers.push(marker);

    // Log for debugging
    console.log('[Marker Created] POI ID: ' + poiId + ', Name: ' + poiName);

    // Attach click handler to the DOM element - use capture phase
    el.addEventListener('click', function(e) {{
      console.log('[Marker Click Event Fired] POI ID: ' + poiId);
      e.stopPropagation();
      // Navigate using standard scheme to ensure WebView catches it
      window.location.href = 'http://poi/selected?id=' + poiId;
    }}, false);

    // Make element explicitly clickable
    el.style.pointerEvents = 'auto';
    el.style.zIndex = '1000';
  }});
}}

function refreshMarkers(newPois) {{
  pois = newPois;
  if (map && map.loaded()) addMarkers(newPois);
}}

function updateUserLocation(lng, lat) {{
  if (!map) return;
  console.log('updateUserLocation: ' + lng + ', ' + lat);
  if (!userMarker) {{
    var el = document.createElement('div');
    el.id = 'user-marker';
    userMarker = new goongjs.Marker({{ element: el, anchor: 'center' }}).setLngLat([lng, lat]).addTo(map);
  }} else {{
    userMarker.setLngLat([lng, lat]);
  }}
  // Ensure marker container stays on top
  var container = userMarker.getElement().parentNode;
  if (container) container.style.zIndex = '9999';
}}

function drawRoute(coords) {{
  console.log('drawRoute called with', coords.length, 'coords');
  if (!map) {{ console.error('drawRoute: map not ready'); return; }}
  
  // Clear old dashed route when redrawing
  if (map.getLayer('dashed-route')) {{ map.removeLayer('dashed-route'); }}
  if (map.getSource('dashed-route')) {{ map.removeSource('dashed-route'); }}
  
  if (map.getSource('route')) {{
    map.getSource('route').setData({{
      'type': 'Feature',
      'properties': {{}},
      'geometry': {{
        'type': 'LineString',
        'coordinates': coords
      }}
    }});
    console.log('drawRoute: updated existing source');
  }} else {{
    map.addSource('route', {{
      'type': 'geojson',
      'data': {{
        'type': 'Feature',
        'properties': {{}},
        'geometry': {{
          'type': 'LineString',
          'coordinates': coords
        }}
      }}
    }});
    map.addLayer({{
      'id': 'route',
      'type': 'line',
      'source': 'route',
      'layout': {{
        'line-join': 'round',
        'line-cap': 'round'
      }},
      'paint': {{
        'line-color': '#2563EB',
        'line-width': 6,
        'line-opacity': 0.85
      }}
    }});
    console.log('drawRoute: created new source+layer');
  }}

  if (coords.length > 0) {{
    var bounds = coords.reduce(function(b, coord) {{
      return b.extend(coord);
    }}, new goongjs.LngLatBounds(coords[0], coords[0]));
    map.fitBounds(bounds, {{ padding: 50 }});
    console.log('drawRoute: fitBounds done');
  }}
}}

function drawDashedRoute(coords) {{
  if (!map || !coords || coords.length < 2) return;
  console.log('drawDashedRoute called with', coords.length, 'segments');
  
  if (map.getSource('dashed-route')) {{
    map.getSource('dashed-route').setData({{
      'type': 'Feature',
      'properties': {{}},
      'geometry': {{ 'type': 'MultiLineString', 'coordinates': coords }}
    }});
  }} else {{
    map.addSource('dashed-route', {{
      'type': 'geojson',
      'data': {{
        'type': 'Feature',
        'properties': {{}},
        'geometry': {{ 'type': 'MultiLineString', 'coordinates': coords }}
      }}
    }});
    map.addLayer({{
      'id': 'dashed-route',
      'type': 'line',
      'source': 'dashed-route',
      'layout': {{
        'line-join': 'round',
        'line-cap': 'round'
      }},
      'paint': {{
        'line-color': '#2563EB',
        'line-width': 4,
        'line-opacity': 0.7,
        'line-dasharray': [2, 3]
      }}
    }});
  }}
}}

function highlightPoi(poiId) {{
  if (!map) return;
  document.querySelectorAll('.poi-marker').forEach(function(m) {{ m.classList.remove('triggered'); m.style.backgroundColor = ''; }});
  document.querySelectorAll('.poi-marker').forEach(function(m) {{
    if (m.getAttribute('data-poi-id') == poiId) m.classList.add('triggered');
  }});
}}

function highlightPoiGreen(poiId) {{
  if (!map) return;
  document.querySelectorAll('.poi-marker').forEach(function(m) {{ m.classList.remove('triggered'); m.style.backgroundColor = ''; }});
  document.querySelectorAll('.poi-marker').forEach(function(m) {{
    if (m.getAttribute('data-poi-id') == poiId) {{
      m.style.backgroundColor = '#16A34A';
      m.classList.add('triggered');
    }}
  }});
}}

function highlightPoisGreen(poiIds) {{
  if (!map) return;
  document.querySelectorAll('.poi-marker').forEach(function(m) {{ m.classList.remove('triggered'); m.style.backgroundColor = ''; }});
  document.querySelectorAll('.poi-marker').forEach(function(m) {{
    var id = parseInt(m.getAttribute('data-poi-id'));
    if (poiIds.includes(id)) {{
      m.style.backgroundColor = '#16A34A';
      m.classList.add('triggered');
    }}
  }});
}}

// Timeout 8s nếu CDN chưa load (tăng lên cho mạng chậm)
window.goongLoadTimeout = setTimeout(function() {{
  if (!_mapInitialized) {{
    if (window.goongjs) {{
      console.log('[Map] Timeout nhưng goongjs có sẵn, init...');
      initMapWithFallback();
    }} else {{
      onGoongLoadError();
    }}
  }}
}}, 8000);
</script>

{jsTag}
{cssTag}
</body>
</html>";
    }
}

using TourApp.Mobile.Models;
using TourApp.Mobile.Services;
using System.Text.Json;

namespace TourApp.Mobile.Views;

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

    // ----------------------------------------------------------------
    //  GOONG KEYS
    //  - MaptileKey : dùng cho goongjs.accessToken (render bản đồ tile)
    //  - RestApiKey : dùng cho rsapi.goong.io/Place (Search, Geocode)
    //    !! Hai key KHÁC NHAU. Lấy tại: https://account.goong.io → My Keys
    // ----------------------------------------------------------------
    private const string GoongMaptileKey = "2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9";
    // TODO: Dán REST API Key vào đây (lấy từ Goong account → API Keys)
    private const string GoongRestApiKey = ""; // để trống = tắt Goong Search

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

            _locationService.LocationChanged += OnLocationChanged;
            _geofenceService.PoiTriggered += OnPoiTriggered;
            _geofenceService.HighlightRequested += (_, poiId) =>
                MapWebView.Eval($"highlightPoi({poiId});");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] CONSTRUCTOR ERROR: {ex}");
            // Fallback — tạo service mặc định để app không crash
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
            LoadPoisInBackgroundAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                    System.Diagnostics.Debug.WriteLine($"[MapPage] POI load faulted: {t.Exception}");
            }, TaskContinuationOptions.OnlyOnFaulted);

            // Bắt đầu tracking GPS
            await _locationService.StartTracking();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage.OnAppearing] ERROR: {ex}");
#if DEBUG
            try { await DisplayAlert("⚠️ Lỗi khởi động", ex.Message, "OK"); }
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

                // Cập nhật GeofenceService
                _geofenceService.SetPois(_pois);

                // Nếu có POI → refresh markers trên map
                if (_pois.Any())
                {
                    var poisJson = System.Text.Json.JsonSerializer.Serialize(_pois);
                    MainThread.BeginInvokeOnMainThread(() =>
                        MapWebView.Eval($"refreshMarkers({poisJson});"));
                }
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
        _locationService.StopTracking();
    }

    private void OnLocationChanged(object? sender, Location location)
    {
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

        MainThread.BeginInvokeOnMainThread(() =>
        {
            MapWebView.Eval($"updateUserLocation({location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");

            if (shouldCheckGeofence)
            {
                _lastCheckedLocation = location;
                _geofenceService.CheckGeofences(location);
            }

            if (BottomSheetView.IsVisible && _currentPoi != null)
            {
                var dist = Location.CalculateDistance(
                    location.Latitude, location.Longitude,
                    _currentPoi.Latitude, _currentPoi.Longitude,
                    DistanceUnits.Kilometers) * 1000;
                PoiDistanceLabel.Text = dist < 1000
                    ? $"• {(int)dist}m từ bạn"
                    : $"• {dist / 1000:F1}km từ bạn";
            }
        });
    }

    private void OnPoiTriggered(object? sender, POI poi)
    {
        MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
    }

    private void ShowPoiDetails(POI poi)
    {
        _currentPoi = poi;
        PoiNameLabel.Text = poi.PoiName;
        PoiDescLabel.Text = poi.Description;
        PoiRatingLabel.Text = $"⭐ {poi.Rating:F1}";
        PoiDistanceLabel.Text = $"• {poi.Radius}m bán kính";

        if (_lastLocation != null)
        {
            var dist = Location.CalculateDistance(
                _lastLocation.Latitude, _lastLocation.Longitude,
                poi.Latitude, poi.Longitude,
                DistanceUnits.Kilometers) * 1000;
            PoiDistanceLabel.Text = dist < 1000 ? $"• {(int)dist}m từ bạn" : $"• {dist / 1000:F1}km từ bạn";
        }

        PoiImage.Source = !string.IsNullOrEmpty(poi.ImageUrl)
            ? ImageSource.FromUri(new Uri(poi.ImageUrl))
            : null;

        bool isFav = Preferences.Default.Get($"fav_{poi.PoiId}", false);
        FavBtn.Text = isFav ? "❤️" : "🤍";

        BottomSheetView.IsVisible = true;
    }

    private void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[MapWebView Navigation] URL: {e.Url}");

        if (e.Url.StartsWith("poi://selected") || e.Url.StartsWith("http://poi/selected"))
        {
            e.Cancel = true;
            System.Diagnostics.Debug.WriteLine($"[MapWebView] POI selected: {e.Url}");

            try
            {
                var uri = new Uri(e.Url);
                var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
                if (int.TryParse(query["id"], out int poiId))
                {
                    System.Diagnostics.Debug.WriteLine($"[MapWebView] Looking for POI ID: {poiId}");
                    var poi = _pois?.FirstOrDefault(p => p.PoiId == poiId);
                    if (poi != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MapWebView] Found POI: {poi.PoiName}");
                        MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MapWebView] POI not found. Available POIs: {string.Join(", ", _pois?.Select(p => $"ID:{p.PoiId}") ?? new[] { "none" })}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapWebView] Error: {ex.Message}");
            }
        }
    }

    private void OnPlayAudioClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        _ = TextToSpeech.Default.SpeakAsync(
            $"Chào mừng bạn đến {_currentPoi.PoiName}. {_currentPoi.Description}");
    }

    private async void OnDirectionsClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        var url = $"https://www.google.com/maps/dir/?api=1&destination={_currentPoi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},{_currentPoi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}&travelmode=walking";
        try { await Launcher.Default.OpenAsync(new Uri(url)); }
        catch { await DisplayAlert("Lỗi", "Không thể mở ứng dụng bản đồ.", "OK"); }
    }

    private void OnFavoriteClicked(object? sender, EventArgs e)
    {
        if (_currentPoi == null) return;
        bool isFav = Preferences.Default.Get($"fav_{_currentPoi.PoiId}", false);
        Preferences.Default.Set($"fav_{_currentPoi.PoiId}", !isFav);
        FavBtn.Text = !isFav ? "❤️" : "🤍";
    }

    private void OnCloseSheetClicked(object? sender, EventArgs e)
    {
        BottomSheetView.IsVisible = false;
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        await DoSearch(SearchEntry.Text);
    }

    private async void OnSearchCompleted(object? sender, EventArgs e)
    {
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
            await DisplayAlert("Lỗi", "Địa chỉ phải bắt đầu bằng http:// hoặc https://", "OK");
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
            await DisplayAlert("✅ Thành công", $"Đã kết nối: {result}\nTải được {_pois.Count} địa điểm.", "OK");
        }
        else
        {
            await DisplayAlert("❌ Không kết nối được",
                $"Không thể kết nối tới:\n{result}\n\n" +
                "Kiểm tra:\n• API đang chạy?\n• Cùng mạng WiFi?\n• IP đúng chưa?\n• Firewall cho phép chưa?",
                "OK");
        }
    }

    private async Task DoSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        // 1. Tìm trong dữ liệu POI local trước
        var local = _pois?.FirstOrDefault(p =>
            p.PoiName.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (local != null)
        {
            ShowPoiDetails(local);
            MapWebView.Eval($"map.flyTo({{center: [{local.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {local.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
            return;
        }

        // 2. Goong Places API (chỉ gọi nếu có REST API key)
        if (string.IsNullOrEmpty(GoongRestApiKey))
        {
            await DisplayAlert("Tìm kiếm", $"Không tìm thấy '{query}' trong danh sách địa điểm.", "OK");
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
        if (_lastLocation != null)
            MapWebView.Eval($"map.flyTo({{center: [{_lastLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {_lastLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
        else
            MapWebView.Eval("map.flyTo({center: [106.7018, 10.7596], zoom: 17});");
    }

    private void LoadMap()
    {
        var poisJson = System.Text.Json.JsonSerializer.Serialize(_pois ?? new List<POI>());
        var html = BuildMapHtml(poisJson);
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private static string BuildMapHtml(string poisJson)
    {
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
  .poi-marker.triggered {{ background:#EF4444; animation:pulse 1s infinite; }}
  @keyframes pulse {{
    0%,100% {{ box-shadow:0 0 0 0 rgba(239,68,68,0.5); }}
    50% {{ box-shadow:0 0 0 12px rgba(239,68,68,0); }}
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
  <p class='sub'>Cần kết nối internet</p>
</div>

<!-- Error fallback -->
<div id='errmsg'>
  <h3>⚠️ Không thể tải bản đồ</h3>
  <p>Goong Maps cần kết nối internet.<br>Kiểm tra WiFi và thử lại.</p>
</div>

<div id='map'></div>

<!-- QUAN TRỌNG: Khai báo hàm TRƯỚC khi load CDN
     Lý do: onerror/onload của script CDN gọi các hàm này ngay khi CDN load xong.
     Nếu khai báo sau, hàm chưa tồn tại → ReferenceError → map trắng -->
<script>
var map, pois = {poisJson}, userMarker = null;

function onGoongLoadError() {{
  clearTimeout(window.goongLoadTimeout);
  document.getElementById('loading').style.display = 'none';
  document.getElementById('errmsg').classList.add('show');
}}

function onGoongLoaded() {{
  clearTimeout(window.goongLoadTimeout);
  try {{
    goongjs.accessToken = '{GoongMaptileKey}';
    map = new goongjs.Map({{
      container: 'map',
      style: 'https://tiles.goong.io/assets/goong_map_web.json',
      center: [106.7018, 10.7596],
      zoom: 16
    }});
    map.on('load', function() {{
      document.getElementById('loading').classList.add('hide');
      addMarkers(pois);
    }});
    map.on('error', function(e) {{
      document.getElementById('loading').classList.add('hide');
    }});
  }} catch(e) {{
    onGoongLoadError();
  }}
}}

function addMarkers(poiList) {{
  if (!map) return;

  poiList.forEach(function(poi) {{
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
      .setLngLat([poi.Longitude, poi.Latitude])
      .addTo(map);

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
  if (!userMarker) {{
    var el = document.createElement('div');
    el.id = 'user-marker';
    userMarker = new goongjs.Marker(el).setLngLat([lng, lat]).addTo(map);
  }} else {{
    userMarker.setLngLat([lng, lat]);
  }}
}}

function highlightPoi(poiId) {{
  if (!map) return;
  document.querySelectorAll('.poi-marker').forEach(function(m) {{ m.classList.remove('triggered'); }});
  document.querySelectorAll('.poi-marker').forEach(function(m) {{
    if (m.getAttribute('data-poi-id') == poiId) m.classList.add('triggered');
  }});
}}

// Timeout 5s nếu CDN chưa load
window.goongLoadTimeout = setTimeout(function() {{
  if (!window.goongjs) onGoongLoadError();
}}, 5000);
</script>

<!-- Load Goong JS CDN - gọi onGoongLoaded/onGoongLoadError đã define ở trên -->
<script
  src='https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.js'
  onload='onGoongLoaded()'
  onerror='onGoongLoadError()'>
</script>
<link href='https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.css' rel='stylesheet'>
</body>
</html>";
    }
}

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
    private Location? _lastCheckedLocation; // For GPS movement throttle

    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly HttpClient _http = new();

    public MapPage()
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

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            // Tránh lỗi nén tài nguyên trên Android cũ khi load quá nhanh lúc bật app
            await Task.Delay(200);
            // 1. Tải bản đồ rỗng trước LẬP TỨC để tránh trắng màn hình (tránh ANR/Crash khi startup)
            if (!_isMapLoaded)
            {
                LoadMap();
                _isMapLoaded = true;
            }

            // [ANTI CRASH] Đợi WebView nạp HTML vào bộ nhớ đệm an toàn rồi mới xin quyền GPS (tránh xung đột vòng đời Activity gây văng app)
            await Task.Delay(1000); 

            // 2. Chạy Tracking GPS ngầm
            await _locationService.StartTracking();

            // 3. Tải POI từ API (nó sẽ chờ Auto-Discovery dò tìm IP ngầm)
            if (_pois == null)
            {
                _pois = await _apiService.GetAllPOIsAsync();

                // Nếu tìm được POI thì đẩy vào MapJS
                if (_pois != null && _pois.Any())
                {
                    var poisJson = JsonSerializer.Serialize(_pois, _jsonOptions);
                    MapWebView.Eval($"refreshMarkers({poisJson});");
                    await _geofenceService.InitializeAsync(); // Cập nhật geofence từ data mới
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MapPage] OnAppearing error: {ex}");
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

        // [FIX-THROTTLE] Chỉ gọi CheckGeofences khi user di chuyển > 5m
        // Tránh spam xử lý khi đứng yên (học từ Vibe Coding Decoupling)
        bool shouldCheckGeofence = true;
        if (_lastCheckedLocation != null)
        {
            var movedMeters = Location.CalculateDistance(
                _lastCheckedLocation.Latitude, _lastCheckedLocation.Longitude,
                location.Latitude, location.Longitude,
                DistanceUnits.Kilometers) * 1000;
            shouldCheckGeofence = movedMeters > 5; // chỉ check khi di chuyển > 5m
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Luôn update vị trí trên map (dot chạy mượt)
            MapWebView.Eval($"updateUserLocation({location.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {location.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)});");

            if (shouldCheckGeofence)
            {
                _lastCheckedLocation = location;
                _geofenceService.CheckGeofences(location);
            }

            // Update distance trên bottom sheet nếu đang mở
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

        // Update distance if we have GPS
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

        // Restore fav button state
        bool isFav = Preferences.Default.Get($"fav_{poi.PoiId}", false);
        FavBtn.Text = isFav ? "❤️" : "🤍";

        BottomSheetView.IsVisible = true;
    }

    private void OnMapWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // JS Bridge: poi://selected?id=1
        if (e.Url.StartsWith("poi://selected"))
        {
            e.Cancel = true;
            var uri = new Uri(e.Url);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
            if (int.TryParse(query["id"], out int poiId))
            {
                var poi = _pois?.FirstOrDefault(p => p.PoiId == poiId);
                if (poi != null)
                    MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
            }
            else
            {
                // Try matching by name from the URL path
                var urlName = e.Url.Replace("poi://selected?name=", "");
                var poi = _pois?.FirstOrDefault(p => Uri.EscapeDataString(p.PoiName) == urlName);
                if (poi != null)
                    MainThread.BeginInvokeOnMainThread(() => ShowPoiDetails(poi));
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

    private async Task DoSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        // First check mock data
        var local = _pois?.FirstOrDefault(p =>
            p.PoiName.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (local != null)
        {
            ShowPoiDetails(local);
            MapWebView.Eval($"map.flyTo({{center: [{local.Longitude}, {local.Latitude}], zoom: 17}});");
            return;
        }

        // Goong Places API
        try
        {
            var encoded = Uri.EscapeDataString(query);
            var url = $"https://rsapi.goong.io/Place/AutoComplete?api_key=2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9&input={encoded}&location=10.759,106.701";
            var res = await _http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(res);
            var preds = doc.RootElement.GetProperty("predictions");
            if (preds.GetArrayLength() > 0)
            {
                var first = preds[0];
                var name = first.GetProperty("description").GetString();
                // fly to result using geocode
                var placeId = first.GetProperty("place_id").GetString();
                var detailUrl = $"https://rsapi.goong.io/Place/Detail?api_key=2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9&place_id={placeId}";
                var detailRes = await _http.GetStringAsync(detailUrl);
                using var detailDoc = JsonDocument.Parse(detailRes);
                var loc = detailDoc.RootElement.GetProperty("result").GetProperty("geometry").GetProperty("location");
                double lat = loc.GetProperty("lat").GetDouble();
                double lng = loc.GetProperty("lng").GetDouble();
                MapWebView.Eval($"map.flyTo({{center: [{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {lat.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");

                // Show info
                PoiNameLabel.Text = name ?? query;
                PoiDescLabel.Text = "Kết quả tìm kiếm từ Goong Maps";
                PoiRatingLabel.Text = "⭐ --";
                PoiDistanceLabel.Text = "";
                PoiImage.Source = null;
                BottomSheetView.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Search] Error: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() => 
            {
                DisplayAlert("Lỗi tìm kiếm", "Không thể tìm kiếm địa điểm lúc này. Vui lòng kiểm tra lại kết nối mạng hoặc thử lại sau.", "OK");
            });
        }
    }

    private void OnRecenterClicked(object? sender, EventArgs e)
    {
        if (_lastLocation != null)
        {
            MapWebView.Eval($"map.flyTo({{center: [{_lastLocation.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {_lastLocation.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], zoom: 17}});");
        }
        else
        {
            // No GPS yet — fly to Vinh Khanh default
            MapWebView.Eval("map.flyTo({center: [106.7018, 10.7596], zoom: 17});");
        }
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
<script src='https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.js'></script>
<link href='https://cdn.jsdelivr.net/npm/@goongmaps/goong-js@1.0.9/dist/goong-js.css' rel='stylesheet'>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  body, html {{ width:100%; height:100%; overflow:hidden; }}
  #map {{ width:100%; height:100%; }}
  .poi-marker {{ 
    width:40px; height:40px; border-radius:50%; background:#4F46E5;
    border:3px solid white; cursor:pointer; display:flex; align-items:center;
    justify-content:center; font-size:18px; box-shadow:0 2px 8px rgba(0,0,0,0.3);
    transition:transform 0.2s;
  }}
  .poi-marker:hover {{ transform:scale(1.2); }}
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
<div id='map'></div>
<script>
goongjs.accessToken = '2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9';
var map = new goongjs.Map({{
  container: 'map',
  style: 'https://tiles.goong.io/assets/goong_map_web.json',
  center: [106.7018, 10.7596],
  zoom: 16
}});

var pois = {poisJson};
var userMarker = null;

map.on('load', function() {{
  addMarkers(pois);
}});

function addMarkers(poiList) {{
  poiList.forEach(function(poi) {{
    var el = document.createElement('div');
    el.className = 'poi-marker';
    // [FIX] C# serialize với JsonPropertyName('id') nên model thành poi.id
    el.setAttribute('data-poi-id', poi.id); 
    el.innerHTML = '🍽️';
    el.addEventListener('click', function() {{
      window.location.href = 'poi://selected?id=' + poi.id;
    }});
    new goongjs.Marker(el).setLngLat([poi.Longitude, poi.Latitude]).addTo(map);
  }});
}}

// [NEW] Gọi từ C# sau khi load POI từ API
function refreshMarkers(newPois) {{
  pois = newPois;
  // Chỉ add marker mới nếu bản đồ đã load
  if (map.loaded()) addMarkers(newPois);
}}

function updateUserLocation(lng, lat) {{
  if (!userMarker) {{
    var el = document.createElement('div');
    el.id = 'user-marker';
    userMarker = new goongjs.Marker(el).setLngLat([lng, lat]).addTo(map);
    // [FIX] Bỏ auto flyTo để bản đồ giữ nguyên mặc định ở Quận 4 theo ý người dùng
  }} else {{
    userMarker.setLngLat([lng, lat]);
  }}
}}

function highlightPoi(poiId) {{
  document.querySelectorAll('.poi-marker').forEach(function(m) {{ m.classList.remove('triggered'); }});
  var allMarkers = document.querySelectorAll('.poi-marker');
  for (var i = 0; i < allMarkers.length; i++) {{
    if (allMarkers[i].getAttribute('data-poi-id') == poiId) {{
      allMarkers[i].classList.add('triggered');
      break;
    }}
  }}
}}
</script>
</body>
</html>";
    }
}
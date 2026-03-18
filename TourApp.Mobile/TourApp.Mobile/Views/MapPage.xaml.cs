using TourApp.Mobile.Models;
using TourApp.Mobile.Services;

namespace TourApp.Mobile.Views
{
    public partial class MapPage : ContentPage
    {
        private readonly DatabaseService _db = new();
        private List<POI> _pois = new();
        private POI _selectedPoi;

        public MapPage()
        {
            InitializeComponent();
            LoadMapAsync();
        }

        private async void LoadMapAsync()
        {
            _pois = await _db.GetAllPOIsAsync();
            var html = BuildMapHtml(_pois);
            MapWebView.Source = new HtmlWebViewSource { Html = html };
        }

        private string BuildMapHtml(List<POI> pois)
        {
            var markers = "";
            foreach (var poi in pois)
            {
                markers += $@"
                    var marker = L.marker([{poi.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 
                                           {poi.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}])
                        .addTo(map)
                        .bindPopup('<b>{poi.PoiName}</b><br>{poi.Address}');
                ";
            }

            return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link rel='stylesheet' href='https://unpkg.com/leaflet@1.9.4/dist/leaflet.css'/>
    <script src='https://unpkg.com/leaflet@1.9.4/dist/leaflet.js'></script>
    <style>
        body {{ margin:0; padding:0; }}
        #map {{ width:100%; height:100vh; }}
    </style>
</head>
<body>
    <div id='map'></div>
    <script>
        var map = L.map('map').setView([10.7769, 106.7009], 13);
  L.tileLayer('https://{{s}}.basemaps.cartocdn.com/rastertiles/voyager/{{z}}/{{x}}/{{y}}{{r}}.png', {{
    attribution: '© OpenStreetMap © CARTO',
    subdomains: 'abcd',
    maxZoom: 19
}}).addTo(map);
        {markers}
    </script>
</body>
</html>";
        }

        private async void OnPlayAudioClicked(object sender, EventArgs e)
        {
            if (_selectedPoi == null) return;
            var audio = await _db.GetAudioByPoiAsync(_selectedPoi.PoiId);
            if (audio != null)
                await DisplayAlert("Thuyết minh", audio.ScriptText, "OK");
        }
    }
}
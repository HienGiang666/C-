namespace TourApp.Mobile.Views;

public partial class MapPage : ContentPage
{
    public MapPage()
    {
        InitializeComponent();
        LoadMap();
    }

    private void LoadMap()
    {
        MapWebView.Source = new HtmlWebViewSource
        {
            Html = GetMapHtml()
        };
    }

    private string GetMapHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <link href='https://cdn.jsdelivr.net/npm/goong-js@1.0.9/dist/goong-js.css' rel='stylesheet'/>
    <script src='https://cdn.jsdelivr.net/npm/goong-js@1.0.9/dist/goong.min.js'></script>
    <style>
        body, html, #map { margin: 0; padding: 0; height: 100%; width: 100%; }
        #debug { position:fixed; top:5px; left:5px; background:red; color:white; 
                 padding:5px; font-size:12px; z-index:999; display:none; }
    </style>
</head>
<body>
    <div id='map'></div>
    <div id='debug'></div>
    <script>
        try {
            goongjs.accessToken = '2Dnp8yaRq6ivkjX5c7D7RFcx5tDSi5g512jA5dG9';
            var map = new goongjs.Map({
                container: 'map',
                style: 'https://tiles.goong.io/assets/goong_map_web.json',
                center: [106.6297, 10.8231],
                zoom: 14
            });
            map.on('error', function(e) {
                document.getElementById('debug').style.display='block';
                document.getElementById('debug').innerText = 'Map error: ' + e.error;
            });
        } catch(e) {
            document.getElementById('debug').style.display='block';
            document.getElementById('debug').innerText = 'JS error: ' + e.message;
        }
    </script>
</body>
</html>";
    }

    private void OnPlayAudioClicked(object sender, EventArgs e)
    {
        // TODO: phát audio
    }


}
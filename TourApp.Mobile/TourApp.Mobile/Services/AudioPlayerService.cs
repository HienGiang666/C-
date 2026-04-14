using Plugin.Maui.Audio;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TourApp.Mobile.Services;

public class AudioPlayerService : INotifyPropertyChanged
{
    private static AudioPlayerService? _instance;
    public static AudioPlayerService Instance => _instance ??= new AudioPlayerService();

    private IAudioPlayer? _player;
    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient;

    private bool _isPlaying;
    public bool IsPlaying
    {
        get => _isPlaying;
        set { _isPlaying = value; OnPropertyChanged(); }
    }

    private string _currentAudioTitle = "";
    public string CurrentAudioTitle
    {
        get => _currentAudioTitle;
        set { _currentAudioTitle = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event EventHandler? PlaybackEnded;

    private AudioPlayerService()
    {
        _audioManager = AudioManager.Current;
        _httpClient = new HttpClient();
    }

    public async Task PlayFromUrlAsync(string url, string title)
    {
        try
        {
            Stop();
            
            CurrentAudioTitle = title;
            Debug.WriteLine($"[AudioPlayerService] Fetching audio from: {url}");
            // Local caching support
            var cacheFileName = $"audio_{Math.Abs(url.GetHashCode())}.mp3";
            var localFile = Path.Combine(FileSystem.CacheDirectory, cacheFileName);

            if (!File.Exists(localFile))
            {
                var response = await _httpClient.GetAsync(url);
                if (!response.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[AudioPlayerService] Failed to load audio: {response.StatusCode}");
                    return;
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = File.Create(localFile))
                {
                    await stream.CopyToAsync(fileStream);
                }
            }
            else
            {
                Debug.WriteLine($"[AudioPlayerService] Playing from cache: {cacheFileName}");
            }

            var localStream = File.OpenRead(localFile);
            _player = _audioManager.CreatePlayer(localStream);
            
            _player.PlaybackEnded += (s, e) => 
            {
                IsPlaying = false;
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
            };

            _player.Play();
            IsPlaying = true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayerService] Error playing audio: {ex.Message}");
            IsPlaying = false;
        }
    }

    public void Pause()
    {
        if (_player != null && _player.IsPlaying)
        {
            _player.Pause();
            IsPlaying = false;
        }
    }

    public void Resume()
    {
        if (_player != null && !_player.IsPlaying)
        {
            _player.Play();
            IsPlaying = true;
        }
    }

    public void TogglePlayPause()
    {
        if (IsPlaying) Pause();
        else Resume();
    }

    public void Stop()
    {
        if (_player != null)
        {
            _player.Stop();
            _player.Dispose();
            _player = null;
        }
        IsPlaying = false;
        CurrentAudioTitle = string.Empty;
    }

    public async Task PrecacheAudioAsync(string url)
    {
        try
        {
            var cacheFileName = $"audio_{Math.Abs(url.GetHashCode())}.mp3";
            var localFile = Path.Combine(FileSystem.CacheDirectory, cacheFileName);

            if (!File.Exists(localFile))
            {
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = File.Create(localFile))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                    Debug.WriteLine($"[AudioPlayerService] Preached audio: {cacheFileName}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayerService] Precache error: {ex.Message}");
        }
    }
}

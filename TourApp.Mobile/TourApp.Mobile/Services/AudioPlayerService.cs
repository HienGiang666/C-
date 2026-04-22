using Plugin.Maui.Audio;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Concurrent;

namespace TourApp.Mobile.Services;

public class AudioQueueItem
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int PoiId { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.Now;
}

public class AudioPlayerService : INotifyPropertyChanged, IDisposable
{
    private static AudioPlayerService? _instance;
    private static readonly object _instanceLock = new();
    public static AudioPlayerService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new AudioPlayerService();
                }
            }
            return _instance;
        }
    }

    private IAudioPlayer? _player;
    private readonly IAudioManager _audioManager;
    private readonly HttpClient _httpClient;
    private readonly ConcurrentQueue<AudioQueueItem> _audioQueue = new();
    private bool _isProcessingQueue = false;
    private readonly object _processingLock = new();
    private CancellationTokenSource? _playbackCts;

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

    private int _queueCount;
    public int QueueCount
    {
        get => _queueCount;
        private set { _queueCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasItemsInQueue)); }
    }

    public bool HasItemsInQueue => QueueCount > 0;

    public IReadOnlyCollection<AudioQueueItem> QueueItems => _audioQueue.ToList().AsReadOnly();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    public event EventHandler? PlaybackEnded;
    public event EventHandler? QueueChanged;
    public event EventHandler<AudioQueueItem>? ItemStarted;
    public event EventHandler<AudioQueueItem>? ItemCompleted;

    private AudioPlayerService()
    {
        _audioManager = AudioManager.Current;
        _httpClient = new HttpClient();
    }

    public async Task PlayFromUrlAsync(string url, string title, int poiId = 0)
    {
        var item = new AudioQueueItem 
        { 
            Url = url, 
            Title = title, 
            PoiId = poiId 
        };
        await EnqueueAsync(item);
    }

    public async Task EnqueueAsync(AudioQueueItem item)
    {
        _audioQueue.Enqueue(item);
        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"[AudioPlayerService] Enqueued: {item.Title}, Queue count: {QueueCount}");

        bool shouldStartProcessing = false;
        lock (_processingLock)
        {
            if (!_isProcessingQueue && !IsPlaying)
            {
                _isProcessingQueue = true;
                shouldStartProcessing = true;
            }
        }

        if (shouldStartProcessing)
        {
            try
            {
                await ProcessQueueAsync();
            }
            finally
            {
                lock (_processingLock)
                {
                    _isProcessingQueue = false;
                }
            }
        }
    }

    public async Task EnqueueRangeAsync(IEnumerable<AudioQueueItem> items)
    {
        foreach (var item in items)
        {
            _audioQueue.Enqueue(item);
        }
        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);

        bool shouldStartProcessing = false;
        lock (_processingLock)
        {
            if (!_isProcessingQueue && !IsPlaying)
            {
                _isProcessingQueue = true;
                shouldStartProcessing = true;
            }
        }

        if (shouldStartProcessing)
        {
            try
            {
                await ProcessQueueAsync();
            }
            finally
            {
                lock (_processingLock)
                {
                    _isProcessingQueue = false;
                }
            }
        }
    }

    private async Task ProcessQueueAsync()
    {
        Debug.WriteLine($"[AudioPlayerService] Starting queue processing, items: {QueueCount}");

        while (_audioQueue.TryDequeue(out var item))
        {
            UpdateQueueCount();
            QueueChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                _playbackCts?.Cancel();
                _playbackCts?.Dispose();
                _playbackCts = new CancellationTokenSource();
                
                await PlayItemAsync(item, _playbackCts.Token);
            }
            catch (OperationCanceledException)
            {
                Debug.WriteLine("[AudioPlayerService] Playback cancelled");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Error playing item {item.Title}: {ex.Message}");
            }
        }

        Debug.WriteLine("[AudioPlayerService] Queue processing completed");
    }

    private async Task PlayItemAsync(AudioQueueItem item, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[AudioPlayerService] Playing: {item.Title}");
        
        CurrentAudioTitle = item.Title;
        ItemStarted?.Invoke(this, item);

        var cacheFileName = $"audio_{Math.Abs(item.Url.GetHashCode())}.mp3";
        var localFile = Path.Combine(FileSystem.CacheDirectory, cacheFileName);

        if (!File.Exists(localFile))
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
            
            var response = await _httpClient.GetAsync(item.Url, linkedCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"[AudioPlayerService] Failed to load audio: {response.StatusCode}");
                return;
            }

            using (var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token))
            using (var fileStream = File.Create(localFile))
            {
                await stream.CopyToAsync(fileStream, linkedCts.Token);
            }
        }
        else
        {
            Debug.WriteLine($"[AudioPlayerService] Playing from cache: {cacheFileName}");
        }

        cancellationToken.ThrowIfCancellationRequested();

        Stream? localStream = null;
        try
        {
            localStream = File.OpenRead(localFile);
            _player = _audioManager.CreatePlayer(localStream);
            
            var tcs = new TaskCompletionSource<bool>();
            
            using var reg = cancellationToken.Register(() => 
            {
                tcs.TrySetCanceled();
                _player?.Stop();
            });
            
            _player.PlaybackEnded += (s, e) => 
            {
                IsPlaying = false;
                ItemCompleted?.Invoke(this, item);
                PlaybackEnded?.Invoke(this, EventArgs.Empty);
                tcs.TrySetResult(true);
            };

            _player.Play();
            IsPlaying = true;

            await tcs.Task;
        }
        catch
        {
            localStream?.Dispose();
            throw;
        }
    }

    public void SkipCurrent()
    {
        if (_player != null)
        {
            _player.Stop();
            IsPlaying = false;
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearQueue()
    {
        while (_audioQueue.TryDequeue(out _)) { }
        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine("[AudioPlayerService] Queue cleared");
    }

    public void StopAndClear()
    {
        Stop();
        ClearQueue();
    }

    public void Dispose()
    {
        try
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            Stop();
            _httpClient?.Dispose();
            while (_audioQueue.TryDequeue(out _)) { }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayerService] Dispose error: {ex.Message}");
        }
    }

    private void UpdateQueueCount()
    {
        QueueCount = _audioQueue.Count;
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
        try
        {
            _playbackCts?.Cancel();
            _playbackCts?.Dispose();
            _playbackCts = null;
            
            if (_player != null)
            {
                _player.Stop();
                _player.Dispose();
                _player = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayerService] Stop error: {ex.Message}");
        }
        finally
        {
            IsPlaying = false;
            CurrentAudioTitle = string.Empty;
            lock (_processingLock)
            {
                _isProcessingQueue = false;
            }
        }
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

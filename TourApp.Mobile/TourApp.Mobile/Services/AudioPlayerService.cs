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
    
    // TTS fallback: khi không có MP3, dùng text + locale để phát TTS trong queue
    public string? TtsText { get; set; }
    public string? TtsLocale { get; set; }
    public bool IsTts => string.IsNullOrEmpty(Url) && !string.IsNullOrEmpty(TtsText);
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
    private readonly HashSet<int> _enqueuedPoiIds = new();
    private readonly object _enqueuedLock = new();
    private int _currentlyPlayingPoiId = -1;
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
        // Chống trùng lặp: bỏ qua nếu POI đã có trong queue hoặc đang phát
        if (item.PoiId > 0)
        {
            lock (_enqueuedLock)
            {
                if (_currentlyPlayingPoiId == item.PoiId || !_enqueuedPoiIds.Add(item.PoiId))
                {
                    Debug.WriteLine($"[AudioQueue] SKIP duplicate POI {item.PoiId}: {item.Title}");
                    return;
                }
            }
        }

        _audioQueue.Enqueue(item);
        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"[AudioQueue] Enqueued: {item.Title} (POI {item.PoiId}), Queue: {QueueCount}");

        await TryStartProcessingAsync();
    }

    public async Task EnqueueRangeAsync(IEnumerable<AudioQueueItem> items)
    {
        int added = 0;
        foreach (var item in items)
        {
            if (item.PoiId > 0)
            {
                lock (_enqueuedLock)
                {
                    if (_currentlyPlayingPoiId == item.PoiId || !_enqueuedPoiIds.Add(item.PoiId))
                    {
                        Debug.WriteLine($"[AudioQueue] SKIP duplicate POI {item.PoiId}: {item.Title}");
                        continue;
                    }
                }
            }
            _audioQueue.Enqueue(item);
            added++;
        }

        if (added == 0) return;

        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine($"[AudioQueue] Enqueued {added} items, Queue: {QueueCount}");

        await TryStartProcessingAsync();
    }

    private async Task TryStartProcessingAsync()
    {
        bool shouldStartProcessing = false;
        lock (_processingLock)
        {
            if (!_isProcessingQueue)
            {
                _isProcessingQueue = true;
                shouldStartProcessing = true;
            }
        }

        if (shouldStartProcessing)
        {
            await ProcessQueueAsync();
        }
    }

    private async Task ProcessQueueAsync()
    {
        Debug.WriteLine($"[AudioQueue] Starting queue processing, items: {QueueCount}");

        try
        {
            while (true)
            {
                if (!_audioQueue.TryDequeue(out var item))
                    break;

                // Track currently playing, remove from enqueued set
                lock (_enqueuedLock)
                {
                    _currentlyPlayingPoiId = item.PoiId;
                    if (item.PoiId > 0)
                        _enqueuedPoiIds.Remove(item.PoiId);
                }

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
                    Debug.WriteLine("[AudioQueue] Playback cancelled");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AudioQueue] Error playing {item.Title}: {ex.Message}");
                }
                finally
                {
                    lock (_enqueuedLock)
                    {
                        _currentlyPlayingPoiId = -1;
                    }
                }
            }
        }
        finally
        {
            lock (_processingLock)
            {
                // Re-check: nếu có item mới enqueue trong lúc kết thúc, tiếp tục xử lý
                if (!_audioQueue.IsEmpty)
                {
                    Debug.WriteLine("[AudioQueue] New items found after loop, reprocessing...");
                    _isProcessingQueue = true;
                    _ = Task.Run(ProcessQueueAsync);
                }
                else
                {
                    _isProcessingQueue = false;
                    Debug.WriteLine("[AudioQueue] Queue processing completed");
                }
            }
        }
    }

    private async Task PlayItemAsync(AudioQueueItem item, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[AudioPlayerService] Playing: {item.Title} (TTS={item.IsTts})");
        
        CurrentAudioTitle = item.Title;
        ItemStarted?.Invoke(this, item);

        // TTS fallback: phát bằng TextToSpeech thay vì MP3
        if (item.IsTts)
        {
            await PlayTtsItemAsync(item, cancellationToken);
            return;
        }

        var cacheFileName = $"audio_{Math.Abs(item.Url.GetHashCode())}.mp3";
        var audioCacheDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
        Directory.CreateDirectory(audioCacheDir);
        var localFile = Path.Combine(audioCacheDir, cacheFileName);

        bool mp3Ready = false;

        if (!File.Exists(localFile))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, cancellationToken);
                
                var response = await _httpClient.GetAsync(item.Url, linkedCts.Token);
                if (response.IsSuccessStatusCode)
                {
                    using (var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token))
                    using (var fileStream = File.Create(localFile))
                    {
                        await stream.CopyToAsync(fileStream, linkedCts.Token);
                    }
                    mp3Ready = true;
                }
                else
                {
                    Debug.WriteLine($"[AudioPlayerService] Failed to load audio: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayerService] Download error: {ex.Message}");
            }
        }
        else
        {
            Debug.WriteLine($"[AudioPlayerService] Playing from cache: {cacheFileName}");
            mp3Ready = true;
        }

        cancellationToken.ThrowIfCancellationRequested();

        // Fallback TTS if MP3 unavailable and TtsText provided
        if (!mp3Ready && !string.IsNullOrWhiteSpace(item.TtsText))
        {
            Debug.WriteLine($"[AudioPlayerService] MP3 unavailable, falling back to TTS");
            await PlayTtsItemAsync(item, cancellationToken);
            return;
        }

        if (!mp3Ready) return;

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

            try
            {
                _player.Play();
                IsPlaying = true;
                Debug.WriteLine($"[AudioPlayer] Player started: {item.Title}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AudioPlayer] _player.Play() failed: {ex.Message}");
                IsPlaying = false;
                tcs.TrySetResult(true);
            }

            await tcs.Task;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayer] Playback exception: {ex.Message}");
            localStream?.Dispose();
            if (!string.IsNullOrWhiteSpace(item.TtsText))
            {
                Debug.WriteLine($"[AudioPlayerService] Playback failed, falling back to TTS");
                await PlayTtsItemAsync(item, cancellationToken);
            }
            else
            {
                throw;
            }
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
        lock (_enqueuedLock)
        {
            _enqueuedPoiIds.Clear();
        }
        UpdateQueueCount();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        Debug.WriteLine("[AudioQueue] Queue cleared");
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
            lock (_enqueuedLock)
            {
                _enqueuedPoiIds.Clear();
                _currentlyPlayingPoiId = -1;
            }
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

    private async Task PlayTtsItemAsync(AudioQueueItem item, CancellationToken cancellationToken)
    {
        try
        {
            IsPlaying = true;

            var locales = await TextToSpeech.Default.GetLocalesAsync();
            var matchedLocale = locales?.FirstOrDefault(l =>
                !string.IsNullOrEmpty(item.TtsLocale) && 
                l.Language.StartsWith(item.TtsLocale, StringComparison.OrdinalIgnoreCase));

            var options = new SpeechOptions
            {
                Pitch = 1.0f,
                Volume = 1.0f,
                Locale = matchedLocale
            };

            await TextToSpeech.Default.SpeakAsync(item.TtsText!, options, cancellationToken);
            
            ItemCompleted?.Invoke(this, item);
            PlaybackEnded?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AudioPlayerService] TTS error: {ex.Message}");
        }
        finally
        {
            IsPlaying = false;
        }
    }

    public async Task PrecacheAudioAsync(string url)
    {
        try
        {
            var cacheFileName = $"audio_{Math.Abs(url.GetHashCode())}.mp3";
            var audioCacheDir = Path.Combine(FileSystem.AppDataDirectory, "audio_cache");
            Directory.CreateDirectory(audioCacheDir);
            var localFile = Path.Combine(audioCacheDir, cacheFileName);

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

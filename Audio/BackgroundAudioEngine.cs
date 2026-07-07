using NAudio.Wave;
using SoundMinimum.Models;

namespace SoundMinimum.Audio;

public class BackgroundAudioEngine : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private Thread? _monitorThread;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private List<BgTrackItem> _tracks = new();
    private int _currentIndex = -1;
    private bool _isPlaying;
    private float _volume = 0.5f;
    private int _deviceId = -1;

    public void SetDeviceId(int deviceId)
    {
        lock (_lock)
        {
            _deviceId = deviceId;
            if (_output != null)
            {
                var wasPlaying = _isPlaying;
                _output.Stop();
                _output.Dispose();
                _output = new WaveOutEvent();
                if (_deviceId >= 0) _output.DeviceNumber = _deviceId;
                if (_reader != null)
                {
                    _output.Init(_reader);
                    if (wasPlaying) _output.Play();
                }
            }
        }
    }

    public bool IsPlaying => _isPlaying;
    public double Volume { get => _volume; set { _volume = (float)Math.Clamp(value, 0, 1); lock (_lock) { if (_reader != null) _reader.Volume = _volume; } } }
    public string CurrentTrack => _reader != null && _currentIndex >= 0 && _currentIndex < _tracks.Count
        ? _tracks[_currentIndex].DisplayName : "";

    public event Action? OnStarted;
    public event Action? OnStopped;

    public void SetTracks(List<BgTrackItem> tracks)
    {
        _tracks = tracks;
    }

    public void Play()
    {
        if (_tracks.Count == 0) return;
        if (_currentIndex < 0) _currentIndex = 0;
        PlayCurrent();
    }

    private void PlayCurrent()
    {
        if (_currentIndex < 0 || _currentIndex >= _tracks.Count) return;
        var track = _tracks[_currentIndex];
        if (!File.Exists(track.FilePath)) return;

        lock (_lock)
        {
            _output?.Stop();
            _reader?.Dispose();
            _reader = new AudioFileReader(track.FilePath) { Volume = _volume };
            if (_output == null)
            {
                _output = new WaveOutEvent();
                if (_deviceId >= 0) _output.DeviceNumber = _deviceId;
            }
            _output.Init(_reader);
            _output.Play();
            _isPlaying = true;
        }

        StartMonitor();
        OnStarted?.Invoke();
    }

    public void Pause()
    {
        lock (_lock)
        {
            if (_output != null && _isPlaying)
            {
                _output.Pause();
                _isPlaying = false;
            }
        }
    }

    public void Resume()
    {
        lock (_lock)
        {
            if (_output == null || _isPlaying) return;

            // Always resume from current position, regardless of where we seeked to
            if (_reader != null)
            {
                _output.Play();
                _isPlaying = true;
            }
            else
            {
                _currentIndex++;
                if (_currentIndex >= _tracks.Count)
                    _currentIndex = 0;
                PlayCurrent();
            }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _output?.Stop();
            _reader?.Dispose();
            _reader = null;
            _currentIndex = -1;
            _isPlaying = false;
        }
        OnStopped?.Invoke();
    }

    public void Seek(double positionSeconds)
    {
        lock (_lock)
        {
            if (_reader != null)
                _reader.CurrentTime = TimeSpan.FromSeconds(
                    Math.Clamp(positionSeconds, 0, _reader.TotalTime.TotalSeconds));
        }
    }

    public void Next()
    {
        if (_tracks.Count == 0) return;
        lock (_lock)
        {
            _currentIndex++;
            if (_currentIndex >= _tracks.Count)
                _currentIndex = 0;
            if (_isPlaying)
                PlayCurrent();
            else if (_currentIndex >= 0 && _currentIndex < _tracks.Count)
                Play(); // Start playing if not already playing
        }
    }

    public void Previous()
    {
        if (_tracks.Count == 0) return;
        lock (_lock)
        {
            _currentIndex--;
            if (_currentIndex < 0)
                _currentIndex = _tracks.Count - 1;
            if (_isPlaying)
                PlayCurrent();
            else if (_currentIndex >= 0 && _currentIndex < _tracks.Count)
                Play(); // Start playing if not already playing
        }
    }

    private void StartMonitor()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _monitorThread = new Thread(() =>
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(250);
                lock (_lock)
                {
                    if (_reader == null || !_isPlaying) continue;
                    if (_reader.Position >= _reader.Length)
                    {
                        _currentIndex++;
                        if (_currentIndex >= _tracks.Count)
                            _currentIndex = 0;
                        PlayCurrent();
                        // Don't return here - continue monitoring in the same thread
                    }
                }
            }
        })
        { IsBackground = true };
        _monitorThread.Start();
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _output?.Stop();
        _output?.Dispose();
        _reader?.Dispose();
    }
}

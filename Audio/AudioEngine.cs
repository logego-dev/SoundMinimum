using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SoundMinimum.Models;

namespace SoundMinimum.Audio;

public class AudioEngine : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _readerA;
    private AudioFileReader? _readerB;
    private MixingSampleProvider? _mixer;
    private Thread? _fadeThread;
    private Thread? _monitorThread;
    private CancellationTokenSource? _cts;
    private readonly object _lock = new();
    private bool _loop;
    private float _volumeA = 1f, _volumeB;
    private int _deviceId = -1;
    private double _masterVol = 1.0;
    private double _currentFadeDuration = 0.0;
    private double _currentFadeProgress = 0.0;
    private List<int> _slaveDeviceIds = new();

    private class SlaveOutput
    {
        public WaveOutEvent Output = null!;
        public AudioFileReader Reader = null!;
    }

    private List<SlaveOutput> _slaves = new();

    public PlayerState State { get; private set; } = PlayerState.Stopped;
    public double CurrentPosition => _readerA?.CurrentTime.TotalSeconds ?? 0;
    public double TotalDuration => _readerA?.TotalTime.TotalSeconds ?? 0;
    public string CurrentFile { get; private set; } = "";
    public double CurrentFadeDuration => _currentFadeDuration;
    public double CurrentFadeProgress => _currentFadeProgress;
    public double MasterVolume
    {
        get => _masterVol;
        set
        {
            _masterVol = Math.Clamp(value, 0, 1);
            lock (_lock)
            {
                if (_output != null) _output.Volume = (float)_masterVol;
                foreach (var s in _slaves)
                    s.Output.Volume = (float)_masterVol;
            }
        }
    }

    public event Action? OnTrackStarted;
    public event Action? OnTrackEnded;
    public event Action? OnAllStopped;
    public event Action<double>? OnPositionUpdated;
    public event Action<bool>? OnPauseChanged;

    public void SetDevices(List<string> deviceNames)
    {
        var ids = new List<int>();
        foreach (var name in deviceNames)
        {
            if (string.IsNullOrEmpty(name)) continue;
            for (int i = 0; i < WaveOut.DeviceCount; i++)
                if (WaveOut.GetCapabilities(i).ProductName == name)
                    ids.Add(i);
        }

        lock (_lock)
        {
            _deviceId = ids.Count > 0 ? ids[0] : -1;
            _slaveDeviceIds = ids.Count > 1 ? ids.Skip(1).ToList() : new List<int>();

            var wasPlaying = State == PlayerState.Playing;
            var pos = CurrentPosition;
            var file = CurrentFile;

            DestroySlaves();

            if (wasPlaying && !string.IsNullOrEmpty(file))
            {
                StopInternal();
                PlayFileInternal(file, pos);
            }
        }
    }

    public void Play(PlaylistItem item, bool halfFade = false)
    {
        lock (_lock)
        {
            var duration = halfFade ? item.FadeDuration / 2.0 : item.FadeDuration;
            if (item.Crossfade && duration > 0 &&
                State == PlayerState.Playing && _readerA != null)
            {
                StartCrossfade(item, duration);
                return;
            }

            _loop = item.Loop;
            _volumeA = (float)item.Volume;
            PlayFileInternal(item.FilePath, 0);
        }
    }

    private void PlayFileInternal(string filePath, double startPosition)
    {
        StopInternal();
        DestroySlaves();
        if (!File.Exists(filePath)) return;

        CurrentFile = filePath;
        _readerA = new AudioFileReader(filePath) { Volume = _volumeA };
        if (startPosition > 0)
            _readerA.CurrentTime = TimeSpan.FromSeconds(startPosition);

        InitMixerAndPlay();
        StartMonitor();
        CreateSlaves(startPosition);
        State = PlayerState.Playing;
        OnTrackStarted?.Invoke();
    }

    private void InitMixerAndPlay()
    {
        _output?.Stop();
        _output?.Dispose();

        _output = new WaveOutEvent();
        if (_deviceId >= 0) _output.DeviceNumber = _deviceId;

        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
        {
            ReadFully = true
        };

        if (_readerA != null)
            _mixer.AddMixerInput(ToMixFormat(_readerA));
        if (_readerB != null)
            _mixer.AddMixerInput(ToMixFormat(_readerB));

        _output.Init(_mixer);
        _output.Volume = (float)_masterVol;
        _output.Play();
    }

    private void CreateSlaves(double startPosition)
    {
        DestroySlaves();
        if (string.IsNullOrEmpty(CurrentFile)) return;

        foreach (var devId in _slaveDeviceIds)
        {
            try
            {
                var reader = new AudioFileReader(CurrentFile) { Volume = _volumeA };
                if (startPosition > 0)
                    reader.CurrentTime = TimeSpan.FromSeconds(startPosition);

                var outDev = new WaveOutEvent { DeviceNumber = devId };
                var conv = ToMixFormat(reader);
                outDev.Init(conv);
                outDev.Volume = (float)_masterVol;
                outDev.Play();

                _slaves.Add(new SlaveOutput { Output = outDev, Reader = reader });
            }
            catch { }
        }
    }

    private void DestroySlaves()
    {
        foreach (var s in _slaves)
        {
            try { s.Output.Stop(); } catch { }
            s.Output.Dispose();
            s.Reader.Dispose();
        }
        _slaves.Clear();
    }

    private void SyncSlaves()
    {
        if (_readerA == null || _slaves.Count == 0) return;
        var pos = _readerA.CurrentTime;
        foreach (var s in _slaves)
        {
            try
            {
                if (Math.Abs((s.Reader.CurrentTime - pos).TotalMilliseconds) > 100)
                    s.Reader.CurrentTime = pos;
            }
            catch { }
        }
    }

    private ISampleProvider ToMixFormat(AudioFileReader reader)
    {
        ISampleProvider p = reader;
        if (p.WaveFormat.Channels == 1)
            p = new MonoToStereoSampleProvider(p);
        if (p.WaveFormat.SampleRate != 44100)
            p = new WdlResamplingSampleProvider(p, 44100);
        return p;
    }

    private void StartCrossfade(PlaylistItem nextItem, double duration)
    {
        if (!File.Exists(nextItem.FilePath)) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _readerB = new AudioFileReader(nextItem.FilePath) { Volume = 0f };
        _volumeB = 0f;
        _volumeA = (float)nextItem.Volume;
        _currentFadeDuration = duration;

        CurrentFile = nextItem.FilePath;
        _loop = nextItem.Loop;

        _mixer?.AddMixerInput(ToMixFormat(_readerB));
        State = PlayerState.Fading;
        _currentFadeProgress = 0.0;

        _fadeThread = new Thread(() =>
        {
            var steps = (int)(duration * 50);
            for (int i = 0; i <= steps; i++)
            {
                if (token.IsCancellationRequested) return;
                var progress = (double)i / steps;
                _currentFadeProgress = progress;
                lock (_lock)
                {
                    if (_readerA != null) _readerA.Volume = (float)(_volumeA * (1.0 - progress));
                    if (_readerB != null) _readerB.Volume = (float)(_volumeB + progress * (_volumeA - _volumeB));
                }
                Thread.Sleep(20);
            }

            if (!token.IsCancellationRequested)
            {
                lock (_lock)
                {
                    if (_readerA != null)
                    {
                        _mixer?.RemoveMixerInput(_readerA);
                        _readerA.Dispose();
                    }
                    _readerA = _readerB;
                    _readerB = null;
                    _volumeB = 0;
                    _currentFadeDuration = 0;
                    _currentFadeProgress = 0.0;
                    State = PlayerState.Playing;
                }
                // Recreate slaves with new track
                lock (_lock) { CreateSlaves(0); }
                StartMonitor();
                OnTrackStarted?.Invoke();
            }
        })
        { IsBackground = true };
        _fadeThread.Start();
    }

    public void Stop()
    {
        lock (_lock) { StopInternal(); _currentFadeDuration = 0; _currentFadeProgress = 0.0; }
        OnAllStopped?.Invoke();
    }

    private void StopInternal()
    {
        _cts?.Cancel();
        _fadeThread = null;
        try { _output?.Stop(); } catch { }

        if (_mixer != null)
        {
            try
            {
                if (_readerA != null) _mixer.RemoveMixerInput(_readerA);
                if (_readerB != null) _mixer.RemoveMixerInput(_readerB);
            }
            catch { }
        }

        _readerA?.Dispose();
        _readerB?.Dispose();
        _readerA = null;
        _readerB = null;
        _mixer = null;
        DestroySlaves();
        CurrentFile = "";
        State = PlayerState.Stopped;
        _currentFadeDuration = 0;
        _currentFadeProgress = 0.0;
    }

    public void Pause()
    {
        bool nowPaused;
        lock (_lock)
        {
            if (State == PlayerState.Playing || State == PlayerState.Fading)
            {
                _output?.Pause();
                foreach (var s in _slaves) s.Output.Pause();
                State = PlayerState.Paused;
                nowPaused = true;
            }
            else if (State == PlayerState.Paused)
            {
                _output?.Play();
                foreach (var s in _slaves) s.Output.Play();
                State = PlayerState.Playing;
                nowPaused = false;
            }
            else return;
        }
        OnPauseChanged?.Invoke(nowPaused);
    }

    public void SetVolume(double volume)
    {
        lock (_lock)
        {
            _volumeA = (float)Math.Clamp(volume, 0, 1);
            if (_readerA != null) _readerA.Volume = _volumeA;
            foreach (var s in _slaves)
                s.Reader.Volume = _volumeA;
        }
    }

    public void SetLoop(bool loop)
    {
        lock (_lock) { _loop = loop; }
    }

    public void Seek(double positionSeconds)
    {
        lock (_lock)
        {
            if (_readerA != null)
                _readerA.CurrentTime = TimeSpan.FromSeconds(
                    Math.Clamp(positionSeconds, 0, _readerA.TotalTime.TotalSeconds));
            SyncSlaves();
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
                Thread.Sleep(50);
                lock (_lock)
                {
                    if (_readerA == null) continue;
                    if (State == PlayerState.Playing)
                        OnPositionUpdated?.Invoke(CurrentPosition);

                    var nearEnd = _readerA.CurrentTime >= _readerA.TotalTime - TimeSpan.FromMilliseconds(200);
                    if (!nearEnd && _readerA.Position >= _readerA.Length - 4096)
                        nearEnd = true;

                    if (nearEnd)
                    {
                        if (_loop)
                        {
                            var file = CurrentFile;
                            var vol = _volumeA;
                            _output?.Stop();
                            _readerA?.Dispose();
                            _readerA = new AudioFileReader(file) { Volume = vol };
                            _output?.Init(_readerA);
                            _output?.Play();
                            // Recreate slaves with new track
                            CreateSlaves(0);
                        }
                        else
                        {
                            State = PlayerState.Stopped;
                            OnTrackEnded?.Invoke();
                            return;
                        }
                    }
                }
            }
        })
        { IsBackground = true };
        _monitorThread.Start();
    }

    private void RecreateOutput()
    {
        _output?.Stop();
        _output?.Dispose();
        _output = new WaveOutEvent();
        if (_deviceId >= 0) _output.DeviceNumber = _deviceId;
    }

    public void Dispose()
    {
        _cts?.Cancel();
        DestroySlaves();
        _output?.Stop();
        _output?.Dispose();
        _readerA?.Dispose();
        _readerB?.Dispose();
    }
}

using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Mp3Player.Core;

/// <summary>
/// One playing track: decodes an MP3, converts it to 44.1 kHz stereo float, and applies a
/// fade-in / fade-out gain envelope. Several voices are summed by the mixer, which is what
/// makes crossfading possible.
/// </summary>
internal sealed class Voice : ISampleProvider, IDisposable
{
    private readonly object _lock = new();
    private readonly AudioFileReader _reader;
    private readonly ISampleProvider _source;
    private readonly int _rate;

    private float _gain = 1f, _target = 1f, _step;
    private bool _endWhenSilent, _suppressEnding, _endingRaised, _disposed;
    private double _leadSeconds;

    /// <summary>Raised on the audio thread when this voice is within the lead time of its end (or has hit the end).</summary>
    public event Action<Voice>? Ending;

    public Voice(string path, int sampleRate)
    {
        _rate = sampleRate;
        FilePath = path;
        _reader = new AudioFileReader(path);
        Duration = _reader.TotalTime;
        try
        {
            ISampleProvider src = _reader;
            if (src.WaveFormat.SampleRate != sampleRate)
                src = new WdlResamplingSampleProvider(src, sampleRate);
            if (src.WaveFormat.Channels == 1)
                src = new MonoToStereoSampleProvider(src);
            else if (src.WaveFormat.Channels != 2)
                throw new NotSupportedException("Only mono and stereo files are supported.");
            _source = src;
        }
        catch
        {
            _reader.Dispose();
            throw;
        }
    }

    public string FilePath { get; }
    public TimeSpan Duration { get; }
    public WaveFormat WaveFormat => _source.WaveFormat;

    public TimeSpan Position
    {
        get { lock (_lock) { return _disposed ? TimeSpan.Zero : _reader.CurrentTime; } }
    }

    public TimeSpan Remaining
    {
        get
        {
            var r = Duration - Position;
            return r < TimeSpan.Zero ? TimeSpan.Zero : r;
        }
    }

    public void Seek(TimeSpan to)
    {
        lock (_lock)
        {
            if (_disposed) return;
            if (to < TimeSpan.Zero) to = TimeSpan.Zero;
            if (to > Duration) to = Duration;
            _reader.CurrentTime = to;
        }
    }

    public void SetLead(double seconds)
    {
        lock (_lock) { _leadSeconds = seconds; }
    }

    public void FadeIn(double seconds)
    {
        lock (_lock)
        {
            if (seconds <= 0) { _gain = _target = 1f; _step = 0; return; }
            _gain = 0f;
            _target = 1f;
            _step = 1f / (float)Math.Max(1.0, seconds * _rate);
        }
    }

    /// <summary>Fades to silence, after which the voice ends itself and is removed from the mixer.</summary>
    public void FadeOutAndEnd(double seconds)
    {
        lock (_lock)
        {
            _suppressEnding = true;     // an outgoing voice must not trigger "next track" again
            _endWhenSilent = true;
            _target = 0f;
            if (seconds <= 0) { _gain = 0f; _step = 0; return; }
            _step = -_gain / (float)Math.Max(1.0, seconds * _rate);
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        lock (_lock)
        {
            if (_disposed) return 0;
            if (_endWhenSilent && _gain <= 0f) return 0;

            int read = _source.Read(buffer, offset, count);

            if (_gain != 1f || _target != 1f)
            {
                for (int i = 0; i < read; i += 2)   // one stereo frame at a time
                {
                    if (_gain != _target)
                    {
                        _gain += _step;
                        if (_step == 0 || (_step > 0 && _gain >= _target) || (_step < 0 && _gain <= _target))
                            _gain = _target;
                    }
                    buffer[offset + i] *= _gain;
                    if (i + 1 < read) buffer[offset + i + 1] *= _gain;
                }
            }

            if (!_endingRaised && !_suppressEnding)
            {
                bool atEnd = read < count;
                if (atEnd || (Duration - _reader.CurrentTime).TotalSeconds <= _leadSeconds)
                {
                    _endingRaised = true;
                    Ending?.Invoke(this);
                }
            }
            return read;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            _reader.Dispose();
        }
    }
}

/// <summary>
/// Plays voices through the sound card: voices -> mixer -> master volume -> WaveOut.
/// The output runs continuously (silence when nothing plays) so pause/resume is trivial.
/// </summary>
internal sealed class AudioEngine : IDisposable
{
    public const int SampleRate = 44100;

    private readonly WaveOutEvent _out;
    private readonly MixingSampleProvider _mixer;
    private readonly VolumeSampleProvider _master;
    private readonly object _lock = new();
    private readonly List<Voice> _voices = new();
    private Voice? _current;

    /// <summary>Raised on the audio thread when the CURRENT track is about to end (marshal to the UI thread!).</summary>
    public event Action<Voice>? TrackEnding;

    public AudioEngine()
    {
        _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(SampleRate, 2)) { ReadFully = true };
        _mixer.MixerInputEnded += OnMixerInputEnded;
        _master = new VolumeSampleProvider(_mixer);
        _out = new WaveOutEvent { DesiredLatency = 120 };
        _out.Init(_master);
        _out.Play();
    }

    public Voice? Current => Volatile.Read(ref _current);
    public TimeSpan Position => Current?.Position ?? TimeSpan.Zero;
    public TimeSpan Duration => Current?.Duration ?? TimeSpan.Zero;

    /// <summary>0..1 linear gain.</summary>
    public void SetVolume(double volume) => _master.Volume = (float)Math.Clamp(volume, 0.0, 1.0);

    /// <summary>
    /// Starts a track. If something is already playing it fades out over <paramref name="transitionSeconds"/>
    /// while the new track fades in. <paramref name="leadSeconds"/> is how long before the end of the new track
    /// <see cref="TrackEnding"/> is raised (the crossfade length; 0 = at the very end).
    /// Throws if the file cannot be decoded.
    /// </summary>
    public void Play(string path, double transitionSeconds, double leadSeconds)
    {
        var v = new Voice(path, SampleRate);

        double half = v.Duration.TotalSeconds / 2;     // short tracks get a shorter crossfade
        transitionSeconds = Math.Clamp(transitionSeconds, 0, half);
        leadSeconds = Math.Clamp(leadSeconds, 0, half);

        v.SetLead(leadSeconds);
        v.Ending += OnVoiceEnding;

        var old = Volatile.Read(ref _current);
        Volatile.Write(ref _current, v);
        lock (_lock) { _voices.Add(v); }

        if (old != null)
        {
            old.FadeOutAndEnd(Math.Min(transitionSeconds, old.Remaining.TotalSeconds));
            if (transitionSeconds > 0) v.FadeIn(transitionSeconds);
        }

        _mixer.AddMixerInput(v);
    }

    public void Pause() => _out.Pause();

    public void Resume()
    {
        if (_out.PlaybackState == PlaybackState.Paused) _out.Play();
    }

    public void Seek(TimeSpan to) => Current?.Seek(to);

    /// <summary>Silences and closes every voice, releasing the files.</summary>
    public void Stop()
    {
        Volatile.Write(ref _current, null);
        List<Voice> toDispose;
        lock (_lock)
        {
            toDispose = new List<Voice>(_voices);
            _voices.Clear();
        }
        _mixer.RemoveAllMixerInputs();
        foreach (var v in toDispose) v.Dispose();
    }

    private void OnVoiceEnding(Voice v)
    {
        if (ReferenceEquals(v, Volatile.Read(ref _current)))
            TrackEnding?.Invoke(v);
    }

    private void OnMixerInputEnded(object? sender, SampleProviderEventArgs e)
    {
        if (e.SampleProvider is Voice v)
        {
            lock (_lock) { _voices.Remove(v); }
            v.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        _out.Stop();
        _out.Dispose();
    }
}

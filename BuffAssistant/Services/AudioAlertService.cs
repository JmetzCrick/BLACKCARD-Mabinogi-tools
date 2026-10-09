using System.IO;
using NAudio.Wave;

namespace BuffAssistant.Services;

/// <summary>Plays one alert at a time. Duplicate requests during playback are ignored.</summary>
public sealed class AudioAlertService : System.IDisposable
{
    private readonly object _lock = new();
    private IWavePlayer? _output;
    private readonly System.Func<IWavePlayer> _createOutput;
    public AudioAlertService(System.Func<IWavePlayer>? createOutput = null) => _createOutput = createOutput ?? (() => new WaveOutEvent());
    private AudioFileReader? _reader;
    public event System.EventHandler? PlaybackFinished;

    public bool IsPlaying { get { lock (_lock) return _output?.PlaybackState == PlaybackState.Playing; } }
    private static float Gain(float volume) => float.IsFinite(volume) ? System.Math.Clamp(volume, 0, 1) : 0;
    public float Volume { get { lock (_lock) return _reader?.Volume ?? 0; } }
    public void SetVolume(float volume) { lock (_lock) { if (_reader is not null) _reader.Volume = Gain(volume); } }

    public void Play(string filePath, float volume = 1f)
    {
        if (!File.Exists(filePath)) return;
        lock (_lock)
        {
            if (_output?.PlaybackState == PlaybackState.Playing) return;
            StopInternal();
            try
            {
                _reader = new AudioFileReader(filePath) { Volume = Gain(volume) };
                _output = _createOutput();
                _output.Init(_reader);
                _output.PlaybackStopped += Output_PlaybackStopped;
                _output.Play();
            }
            catch { StopInternal(); throw; }
        }
    }

    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_lock)
        {
            if (!ReferenceEquals(sender, _output)) return;
            StopInternal();
        }
        PlaybackFinished?.Invoke(this, System.EventArgs.Empty);
    }

    private void StopInternal()
    {
        var output = _output;
        _output = null;
        if (output is not null)
        {
            output.PlaybackStopped -= Output_PlaybackStopped;
            try { output.Stop(); } catch { }
            output.Dispose();
        }
        _reader?.Dispose();
        _reader = null;
    }

    public void Dispose() { lock (_lock) StopInternal(); }
}

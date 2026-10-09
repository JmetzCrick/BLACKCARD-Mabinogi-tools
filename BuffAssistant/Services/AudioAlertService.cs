using System.IO;
using NAudio.Wave;

namespace BuffAssistant.Services;

/// <summary>Plays one alert at a time. Duplicate requests during playback are ignored.</summary>
public sealed class AudioAlertService : System.IDisposable
{
    private readonly object _lock = new();
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    public event System.EventHandler? PlaybackFinished;

    public bool IsPlaying { get { lock (_lock) return _output?.PlaybackState == PlaybackState.Playing; } }
    public float Volume { get { lock (_lock) return _output?.Volume ?? 0; } }
    public void SetVolume(float volume) { lock (_lock) { if (_output is not null) _output.Volume = System.Math.Clamp(volume, 0, 1); } }

    public void Play(string filePath, float volume = 1f)
    {
        if (!File.Exists(filePath)) return;
        lock (_lock)
        {
            if (_output?.PlaybackState == PlaybackState.Playing) return;
            StopInternal();
            _reader = new AudioFileReader(filePath);
            _output = new WaveOutEvent();
            _output.Init(_reader);
            _output.Volume = System.Math.Clamp(volume, 0f, 1f);
            _output.PlaybackStopped += Output_PlaybackStopped;
            _output.Play();
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

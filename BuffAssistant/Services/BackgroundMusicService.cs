using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NAudio.Wave;

namespace BuffAssistant.Services;

public sealed class BackgroundMusicService : IDisposable
{
    private readonly object _sync = new();
    private IWavePlayer? _output;
    private readonly Func<IWavePlayer> _createOutput;
    public BackgroundMusicService(Func<IWavePlayer>? createOutput = null) => _createOutput = createOutput ?? (() => new WaveOutEvent());
    private AudioFileReader? _reader;
    private List<string> _playlist = new();
    private int _nextIndex;
    private bool _disposed;
    private double _volumePercent = 10;
    private bool _muted;
    private bool _requestedPlaying;
    private bool _alertActive;
    public bool IsPlaybackRequested { get { lock (_sync) return _requestedPlaying; } }
    public bool IsMuted { get { lock (_sync) return _muted; } }
    public float Volume { get { lock (_sync) return _reader?.Volume ?? 0; } }
    public bool Repeat { get; set; } = true;
    public bool IsPlaying { get { lock (_sync) return _output?.PlaybackState == PlaybackState.Playing; } }
    public bool IsPaused { get { lock (_sync) return _output?.PlaybackState == PlaybackState.Paused; } }
    public string? CurrentFile { get; private set; }

    public void PlayPlaylist(IReadOnlyList<string> files, string startFile, double volumePercent)
    {
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var valid = files.Where(File.Exists).ToList();
            if (valid.Count == 0) throw new FileNotFoundException("재생할 음악 파일이 없습니다.");
            DisposeCurrent();
            _playlist = valid;
            _volumePercent = double.IsFinite(volumePercent) ? Math.Clamp(volumePercent, 0, 100) : 0;
            _requestedPlaying = true;
            _nextIndex = Math.Max(0, valid.FindIndex(p => p.Equals(startFile, StringComparison.OrdinalIgnoreCase)));
            StartNext();
        }
    }
    public void Pause() { lock (_sync) { _requestedPlaying = false; _output?.Pause(); } }
    public void Resume() { lock (_sync) { _requestedPlaying = true; ApplyVolume(); if (!_alertActive) _output?.Play(); } }
    public void SetAlertActive(bool active)
    {
        lock (_sync)
        {
            if (_disposed) return;
            _alertActive = active;
            ApplyVolume();
            if (active || !_requestedPlaying) _output?.Pause();
            else _output?.Play();
        }
    }
    public void SetVolumePercent(double percent) { lock (_sync) { _volumePercent = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0; ApplyVolume(); } }
    public void SetMuted(bool muted) { lock (_sync) { _muted = muted; ApplyVolume(); } }
    // WaveOut.Volume changes Windows' shared application/device volume. Scale only this stream.
    private void ApplyVolume() { if (_reader is not null) _reader.Volume = _muted ? 0 : (float)(_volumePercent / 100); }
    private void StartNext()
    {
        DisposeCurrent();
        if (_nextIndex >= _playlist.Count)
        {
            if (!Repeat) { CurrentFile = null; _requestedPlaying = false; return; }
            _nextIndex = 0;
        }
        CurrentFile = _playlist[_nextIndex++];
        try
        {
            _reader = new AudioFileReader(CurrentFile);
            ApplyVolume(); // Set gain before initialization queues the first audio buffer.
            _output = _createOutput();
            _output.Init(_reader);
            ApplyVolume();
            _output.PlaybackStopped += Output_PlaybackStopped;
            if (_requestedPlaying && !_alertActive) _output.Play();
        }
        catch { DisposeCurrent(); CurrentFile = null; throw; }
    }
    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        lock (_sync)
        {
            if (_disposed || !ReferenceEquals(sender, _output)) return;
            if (e.Exception is not null) { DisposeCurrent(); CurrentFile = null; return; }
            try { StartNext(); } catch { CurrentFile = null; }
        }
    }
    private void DisposeCurrent()
    {
        var output = _output;
        _output = null;
        if (output is not null) { output.PlaybackStopped -= Output_PlaybackStopped; try { output.Stop(); } catch { } output.Dispose(); }
        _reader?.Dispose();
        _reader = null;
    }
    public void Stop() { lock (_sync) { _requestedPlaying = false; DisposeCurrent(); CurrentFile = null; } }
    public void Dispose() { lock (_sync) { _disposed = true; DisposeCurrent(); CurrentFile = null; } }
}


using System;
using System.IO;
using System.Collections.Generic;
using BuffAssistant.Services;
using NAudio.Wave;

internal static class AudioVolumeTests
{
    // No hardware output: inspect decoded PCM, and fail on any shared mixer volume write.
    private sealed class Output : IWavePlayer
    {
        public PlaybackState PlaybackState { get; private set; }
        public float Volume { get => 1; set => throw new Exception("Shared output volume must never change"); }
        public IWaveProvider Source;
        public WaveFormat OutputWaveFormat => Source.WaveFormat;
        public event EventHandler<StoppedEventArgs> PlaybackStopped;
        public void Init(IWaveProvider source) { Source = source; }
        public void Play() { PlaybackState = PlaybackState.Playing; }
        public void Pause() { PlaybackState = PlaybackState.Paused; }
        public void Stop() { PlaybackState = PlaybackState.Stopped; }
        public void Dispose() { }
        public void Finish() { Stop(); PlaybackStopped?.Invoke(this, new StoppedEventArgs()); }
        public float Sample()
        {
            var bytes = new byte[4];
            if (Source.Read(bytes, 0, 4) != 4) throw new Exception("Missing PCM sample");
            return BitConverter.ToSingle(bytes);
        }
    }
    public static void Run(Action<bool,string> check)
    {
        var file = Path.Combine(Path.GetTempPath(), "blackcard-audio-" + Guid.NewGuid() + ".wav");
        try
        {
            using (var writer = new WaveFileWriter(file, new WaveFormat(8000,16,1)))
                for (var i=0;i<8000;i++) writer.WriteSample(0.5f);
            var outputs = new List<Output>();
            using var music = new BackgroundMusicService(() => { var o=new Output(); outputs.Add(o); return o; });
            Output alarm = null;
            using var alert = new AudioAlertService(() => alarm = new Output());
            music.PlayPlaylist(new[]{file},file,10);
            bool Near(float value,float expected) => Math.Abs(value-expected)<0.0001;
            check(Near(outputs[^1].Sample(),0.05f),"First BGM buffer is attenuated to configured ten percent");
            for (var i=0;i<20;i++)
            {
                alert.Play(file,1);
                check(Near(alarm.Sample(),0.5f) && Near(outputs[^1].Sample(),0.05f),"Full alert leaves BGM PCM unchanged " + i);
                alert.SetVolume(0.25f);
                check(Near(alarm.Sample(),0.125f) && Near(outputs[^1].Sample(),0.05f),"Independent live alarm gain " + i);
                alarm.Finish();
                outputs[^1].Finish();
                check(Near(outputs[^1].Sample(),0.05f),"Next song retains BGM gain " + i);
            }
            music.SetMuted(true); alert.Play(file,1);
            check(outputs[^1].Sample()==0,"Full-volume alarm cannot unmute BGM");
            music.SetAlertActive(true); music.SetAlertActive(false);
            check(outputs[^1].Sample()==0,"Resume after alarm retains mute");
            music.Pause(); alarm.Finish(); music.SetAlertActive(false);
            check(!music.IsPlaying,"Alarm completion preserves manual pause");
            music.SetMuted(false); music.Resume();
            check(Near(outputs[^1].Sample(),0.05f),"Unmute restores configured gain");
            music.SetVolumePercent(double.NaN);
            check(outputs[^1].Sample()==0,"Invalid gain fails silent");
            alert.Play(file,float.NaN);
            check(alarm.Sample()==0,"Invalid alert gain fails silent");
        }
        finally { File.Delete(file); }
    }
}

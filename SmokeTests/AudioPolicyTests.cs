using System;
using System.IO;
using System.Threading;
using BuffAssistant.Services;

internal static class AudioPolicyTests
{
    public static void Run(Action<bool, string> check)
    {
        var oldSettings = System.Text.Json.JsonSerializer.Deserialize<BuffAssistant.Models.AppSettings>("{\"MusicEnabled\":true,\"MusicFiles\":[\"old-track.mp3\"]}")!;
        check(!oldSettings.ShouldPlayMusic, "Legacy settings with lost manual stop do not auto-start music on upgrade");
        var stoppedSettings = new BuffAssistant.Models.AppSettings { MusicEnabled = false, MusicPlaybackStateSaved = true };
        var restoredSettings = System.Text.Json.JsonSerializer.Deserialize<BuffAssistant.Models.AppSettings>(System.Text.Json.JsonSerializer.Serialize(stoppedSettings))!;
        check(!restoredSettings.ShouldPlayMusic, "Saved manual music stop survives settings reload");
        check(new BuffAssistant.Models.AppSettings().ShouldPlayMusic, "Fresh installations keep original automatic music behavior");
        check(new BuffAssistant.Models.AppSettings().StartupMusicVolumePercent == 10, "Fresh startup music volume is ten percent");
        var legacyVolume = System.Text.Json.JsonSerializer.Deserialize<BuffAssistant.Models.AppSettings>("{\"MusicVolumeDb\":-0.2}")!;
        check(legacyVolume.StartupMusicVolumePercent == 10, "Old decibel setting upgrades to ten percent");
        check(new BuffAssistant.Models.AppSettings { MusicVolumePercent = 50 }.StartupMusicVolumePercent == 10, "Existing loud volume is reduced on first upgrade");
        check(new BuffAssistant.Models.AppSettings { MusicVolumePercent = 5 }.StartupMusicVolumePercent == 5, "Upgrade preserves a quieter music setting");
        check(new BuffAssistant.Models.AppSettings { MusicVolumePercent = 35, MusicVolumeDefaultsVersion = 1 }.StartupMusicVolumePercent == 35, "Subsequent user volume choice survives restart");
        var separateVolumes = new BuffAssistant.Models.AppSettings { MusicVolumePercent = 18, AlertVolumePercent = 25 };
        var restoredVolumes = System.Text.Json.JsonSerializer.Deserialize<BuffAssistant.Models.AppSettings>(System.Text.Json.JsonSerializer.Serialize(separateVolumes))!;
        check(restoredVolumes.MusicVolumePercent == 18 && restoredVolumes.AlertVolumePercent == 25, "Independent music and alert volume settings survive reload");
        var catalogDirectory = Path.Combine(AppContext.BaseDirectory, "music-dedupe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(catalogDirectory);
        try
        {
            var currentTrack = Path.Combine(catalogDirectory, "Etain.mp3");
            var oldTrack = Path.Combine(catalogDirectory, "old-Etain.mp3");
            var differentTrack = Path.Combine(catalogDirectory, "other.mp3");
            File.WriteAllBytes(currentTrack, new byte[] { 1, 2, 3 });
            File.Copy(currentTrack, oldTrack);
            File.WriteAllBytes(differentTrack, new byte[] { 4, 5, 6 });
            var deduped = MusicCatalog.Normalize(new[] { oldTrack, currentTrack, oldTrack, differentTrack }, currentTrack);
            check(deduped.Count == 2 && deduped[0] == currentTrack, "Same audio in old build folders collapses to current bundled track");
            check(MusicCatalog.Normalize(deduped, currentTrack).Count == 2, "Repeated startup normalization does not grow playlist");
        }
        finally { foreach (var file in Directory.GetFiles(catalogDirectory)) File.Delete(file); Directory.Delete(catalogDirectory); }
        var musicFile = Path.Combine(AppContext.BaseDirectory, "Assets", "Music", "Etain.mp3");
        using var music = new BackgroundMusicService();
        music.SetMuted(true);
        music.PlayPlaylist(new[] { musicFile }, musicFile, 50);
        music.SetAlertActive(true);
        check(!music.IsPlaying && music.IsPlaybackRequested, "Background music stops during alert without losing playback intent");
        music.SetAlertActive(false);
        check(music.IsPlaying && music.IsMuted, "Previously playing music resumes with mute preserved");
        music.Pause();
        music.SetAlertActive(true);
        music.SetAlertActive(false);
        check(!music.IsPlaying && !music.IsPlaybackRequested, "Manually paused music never restarts after alert");
        music.Resume();
        music.SetAlertActive(true);
        music.Pause();
        music.SetAlertActive(false);
        check(!music.IsPlaying && !music.IsPlaybackRequested, "Stopping music during alert prevents automatic resume");
        music.SetAlertActive(true);
        music.PlayPlaylist(new[] { musicFile }, musicFile, 50);
        check(!music.IsPlaying, "Selecting or starting a track during alert cannot overlap voice");
        music.Pause();
        music.SetAlertActive(false);
        check(!music.IsPlaying, "Stopped new track remains stopped after alert");
        music.Stop();
        music.SetAlertActive(true);
        music.SetAlertActive(false);
        check(!music.IsPlaying && music.CurrentFile == null, "Alert cannot start music when no track is active");

        var shortTrack = Path.Combine(AppContext.BaseDirectory, "alert-policy-test.wav");
        using (var writer = new NAudio.Wave.WaveFileWriter(shortTrack, new NAudio.Wave.WaveFormat(8000, 16, 1)))
            writer.Write(new byte[1600], 0, 1600);
        using (var alert = new AudioAlertService())
        using (var finished = new ManualResetEventSlim())
        {
            alert.PlaybackFinished += (_, _) => { music.SetAlertActive(false); finished.Set(); };
            music.Resume();
            music.PlayPlaylist(new[] { musicFile }, musicFile, 50);
            music.SetAlertActive(true);
            alert.Play(shortTrack, 0);
            check(!music.IsPlaying && alert.IsPlaying, "Voice and background playback do not overlap");
            check(finished.Wait(3000) && music.IsPlaying, "Natural alert completion resumes previously playing music");
            finished.Reset();
            music.Pause();
            music.SetAlertActive(true);
            alert.Play(shortTrack, 0);
            check(finished.Wait(3000) && !music.IsPlaying, "Natural alert completion keeps manually stopped music silent");
        }
        using (var alertVolume = new AudioAlertService())
        {
            alertVolume.Play(musicFile, 0);
            alertVolume.SetVolume(0.01f);
            check(Math.Abs(alertVolume.Volume - 0.01f) < 0.001 && music.IsMuted, "Live alert volume changes leave background music mute unchanged");
            alertVolume.SetVolume(0);
        }
        File.Delete(shortTrack);
    }
}


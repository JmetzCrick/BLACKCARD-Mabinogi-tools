namespace BuffAssistant.Models;

public sealed class AppSettings
{
    public List<string> MusicFiles { get; set; } = new();
    public bool MusicEnabled { get; set; } = true;
    public bool MusicPlaybackStateSaved { get; set; }
    public bool ShouldPlayMusic => MusicEnabled && (MusicPlaybackStateSaved ||
        (MusicFiles.Count == 0 && BuffRules.Count == 0));
    public double MusicVolumePercent { get; set; } = 10;
    public int MusicVolumeDefaultsVersion { get; set; }
    public double StartupMusicVolumePercent => MusicVolumeDefaultsVersion >= 1 ? System.Math.Clamp(MusicVolumePercent, 0, 100) : System.Math.Min(System.Math.Clamp(MusicVolumePercent, 0, 100), 10);
    public double AlertVolumePercent { get; set; } = 100;
    public double WindowTransparencyPercent { get; set; }
    public bool GatheringAlarmsEnabled { get; set; }
    public List<string> GatheringAlarmItems { get; set; } = new();
    public double GatheringVolumePercent { get; set; } = 50;
    public string UpdateFeedUrl { get; set; } = "";
    public bool MusicRepeat { get; set; } = true;
    public bool MusicMuted { get; set; }
    public string? SelectedMusicFile { get; set; }
    public List<SavedBuffRule> BuffRules { get; set; } = new();
}

public sealed class SavedBuffRule
{
    public string Name { get; set; } = "";
    public string AudioFile { get; set; } = "";
    public double RedPixelRatioThreshold { get; set; } = 0.12;
    public int RequiredConsecutiveDetections { get; set; } = 2;
}


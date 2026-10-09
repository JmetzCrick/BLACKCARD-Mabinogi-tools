using System.IO;
namespace BuffAssistant.Models;
// Display-only row for the unavailable music-buff interface.
public sealed class BuffRule
{
    public string Name { get; set; } = "";
    public string AudioFile { get; set; } = "";
    public string ThresholdDescription => "정확히 30초";
    public string AudioFileShort => string.IsNullOrWhiteSpace(AudioFile) ? "(미지정)" : Path.GetFileName(AudioFile);
}

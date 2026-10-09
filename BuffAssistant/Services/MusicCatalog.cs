using System.IO;
using System.Security.Cryptography;

namespace BuffAssistant.Services;

public static class MusicCatalog
{
    public static readonly string[] BundledNames = ["Pola.mp3", "Ater.mp3", "Etain.mp3", "Dunbarton.mp3", "Vertrag.mp3", "Aine.mp3"];
    public static IReadOnlyList<string> StartupPlaylist(IEnumerable<string> saved, string musicFolder)
    {
        var bundled = BundledNames.Select(n => Path.Combine(musicFolder, n)).Where(File.Exists).ToArray();
        Random.Shared.Shuffle(bundled);
        var hasEtain = bundled.Any(p => Path.GetFileName(p).Equals("Etain.mp3", StringComparison.OrdinalIgnoreCase));
        // Replaced Etain has new MP3 tags; prefer the bundled copy even if an old copy hashes differently.
        var extras = saved.Where(p => !hasEtain || !(Path.GetFileName(p).Equals("Etain.mp3", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(p).Contains("NPC Etain BGM", StringComparison.OrdinalIgnoreCase)));
        return Normalize(bundled.Concat(extras));
    }
    public static string DisplayName(string path) => Path.GetFileName(path).ToLowerInvariant() switch
    {
        "pola.mp3" => "마음 가는 대로 만들 거야 · 폴라",
        "ater.mp3" => "발자국은 이내 모래에 묻히고 · 아테르",
        "etain.mp3" => "여름 들장미의 향기 · 에탄",
        "dunbarton.mp3" => "던바튼 낮 · 이터니티 프로젝트",
        "vertrag.mp3" => "대륙의 과거를 가르는 도끼 · 페타크",
        "aine.mp3" => "칵테일 잔에 비치는 눈웃음 · 아이네",
        _ => Path.GetFileName(path)
    };
    public static IReadOnlyList<string> Normalize(IEnumerable<string> paths, string? bundled = null)
    {
        var result = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenAudio = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in (string.IsNullOrEmpty(bundled) ? paths : new[] { bundled }.Concat(paths)))
        {
            try
            {
                var fullPath = Path.GetFullPath(path);
                if (!File.Exists(fullPath) || !seenPaths.Add(fullPath)) continue;
                using var stream = File.OpenRead(fullPath);
                if (seenAudio.Add(Convert.ToHexString(SHA256.HashData(stream)))) result.Add(fullPath);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
        }
        return result;
    }
}

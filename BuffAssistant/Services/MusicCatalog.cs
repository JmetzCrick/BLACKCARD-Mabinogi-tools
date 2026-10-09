using System.IO;
using System.Security.Cryptography;

namespace BuffAssistant.Services;

public static class MusicCatalog
{
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

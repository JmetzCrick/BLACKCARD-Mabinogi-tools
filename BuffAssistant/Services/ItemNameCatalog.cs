using System.IO;
using System.Text.Json;

namespace BuffAssistant.Services;

public sealed class ItemNameCatalog
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _cachePath;
    private CatalogData _data = new();
    private bool _refreshing;
    public ItemNameCatalog()
    {
        _cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BuffAssistant", "auction-names.json");
        Load(Path.Combine(AppContext.BaseDirectory, "Assets", "auction-names.json"));
        Load(_cachePath);
    }
    private void Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return;
            var data = JsonSerializer.Deserialize<CatalogData>(File.ReadAllText(path));
            if (data is null) return;
            Add(data.Names);
            if (data.UpdatedAt >= _data.UpdatedAt) _data = data;
        }
        catch { /* Bundled names remain available if the local cache is damaged. */ }
    }
    public void Add(IEnumerable<string> names)
    {
        foreach (var name in names.Where(n => !string.IsNullOrWhiteSpace(n))) _names.Add(name.Trim());
    }
    public IReadOnlyList<string> Suggest(string query, int limit = 50) => MatchNames(_names, query, limit);
    public static IReadOnlyList<string> MatchNames(IEnumerable<string> names, string query, int limit = 50)
    {
        static string Normalize(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        var normalized = Normalize(query);
        if (normalized.Length == 0 || limit <= 0) return Array.Empty<string>();
        return names.Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim()).Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(n => Normalize(n).Contains(normalized, StringComparison.OrdinalIgnoreCase))
            .OrderBy(n => !Normalize(n).StartsWith(normalized, StringComparison.OrdinalIgnoreCase))
            .ThenBy(n => n.Length).ThenBy(n => n, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit).ToArray();
    }
    public async Task RefreshAsync(AuctionService service, Action changed, CancellationToken cancellationToken)
    {
        if (_refreshing || (_data.Complete && DateTimeOffset.UtcNow - _data.UpdatedAt < TimeSpan.FromDays(1))) return;
        _refreshing = true;
        try
        {
            // Continue incomplete snapshots in small batches; typing never sends API requests.
            if (_data.Complete || DateTimeOffset.UtcNow - _data.UpdatedAt > TimeSpan.FromDays(1)) _data.NextCursor = null;
            for (var i = 0; i < 20; i++)
            {
                var page = await service.GetCatalogPageAsync(_data.NextCursor, cancellationToken);
                Add(page.Items.Select(item => item.Name));
                _data.NextCursor = page.NextCursor;
                _data.Complete = string.IsNullOrEmpty(page.NextCursor);
                _data.UpdatedAt = DateTimeOffset.UtcNow;
                changed();
                if (_data.Complete) break;
                await Task.Delay(500, cancellationToken);
            }
        }
        finally
        {
            _refreshing = false;
            try
            {
                _data.Names = _names.OrderBy(n => n).ToList();
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                var temporary = _cachePath + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(_data));
                File.Move(temporary, _cachePath, true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
    private sealed class CatalogData
    {
        public List<string> Names { get; set; } = new();
        public string? NextCursor { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public bool Complete { get; set; }
    }
}

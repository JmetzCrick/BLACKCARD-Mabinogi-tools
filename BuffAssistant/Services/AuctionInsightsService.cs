using System.IO;
using System.Text;
using System.Text.Json;

namespace BuffAssistant.Services;

public sealed class AuctionInsights
{
    public string Name { get; init; } = "";
    public DateTimeOffset CheckedAt { get; init; }
    public long? Stock { get; init; }
    public int ListingCount { get; init; }
    public long? LowestPrice { get; init; }
    public bool StockComplete { get; init; }
    public bool HistoryComplete { get; init; }
    public IReadOnlyList<AuctionTrade> Trades { get; init; } = Array.Empty<AuctionTrade>();
    public string Error { get; init; } = "";
    public string Report
    {
        get
        {
            var text = new StringBuilder();
            text.AppendLine($"{Name} · {CheckedAt.ToLocalTime():MM/dd HH:mm:ss} 조회");
            text.AppendLine(Stock is null ? "현재 물량: 조회 실패" : $"{(StockComplete ? "현재 등록 물량" : "조회된 등록 물량 (일부)")}: {Stock:N0}개 · {ListingCount:N0}건");
            text.AppendLine(LowestPrice is null ? "현재 최저가: 매물 없음 또는 조회 실패" : $"현재 개당 최저가: {LowestPrice:N0} 골드");
            var latest = Trades.OrderByDescending(t => t.BoughtAt).FirstOrDefault();
            text.AppendLine(latest is null ? "최근 판매: 확인된 거래 기록 없음" : $"최근 판매: 개당 {latest.Price:N0} 골드 · {latest.Count:N0}개 · {latest.BoughtAt?.ToLocalTime():MM/dd HH:mm}");
            text.AppendLine("날짜별 거래 수량 (프로그램에서 확인한 기록)");
            foreach (var day in Trades.Where(t => t.BoughtAt.HasValue).GroupBy(t => t.BoughtAt!.Value.ToLocalTime().Date).OrderByDescending(g => g.Key))
                text.AppendLine($"  {day.Key:yyyy-MM-dd}: {day.Sum(t => t.Count):N0}개 · {day.Count():N0}건");
            text.AppendLine("API 제공 범위: 최근 1시간 · 이전 날짜는 저장된 관측 기록");
            if (!HistoryComplete) text.AppendLine("최근 거래 조회가 완료되지 않아 기록이 일부 누락될 수 있습니다.");
            if (!string.IsNullOrEmpty(Error)) text.AppendLine(Error);
            return text.ToString().TrimEnd();
        }
    }
}

public sealed class AuctionInsightsService
{
    private readonly IAuctionDataSource _source;
    private readonly string _historyPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, AuctionInsights> _cache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AuctionTrade> _history = new(StringComparer.Ordinal);
    public AuctionInsightsService(IAuctionDataSource source, string? historyPath = null)
    {
        _source = source;
        _historyPath = historyPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BuffAssistant", "auction-history.json");
        try
        {
            if (File.Exists(_historyPath)) MergeTrades(_history, JsonSerializer.Deserialize<List<AuctionTrade>>(File.ReadAllText(_historyPath)) ?? new());
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        catch (JsonException) { }
    }
    public static void MergeTrades(IDictionary<string, AuctionTrade> history, IEnumerable<AuctionTrade> incoming)
    {
        var earliest = DateTimeOffset.UtcNow.AddDays(-30);
        foreach (var trade in incoming.Where(t => t.BoughtAt >= earliest && t.Count > 0)) history[trade.Identity] = trade;
        foreach (var key in history.Where(p => p.Value.BoughtAt < earliest).Select(p => p.Key).ToArray()) history.Remove(key);
    }
    public async Task<AuctionInsights> LoadAsync(string name, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cache.TryGetValue(name, out var cached) && DateTimeOffset.UtcNow - cached.CheckedAt < TimeSpan.FromMinutes(1)) return cached;
            long? stock = null, lowest = null;
            var listingCount = 0;
            var stockComplete = false;
            var historyComplete = false;
            var errors = new List<string>();
            try
            {
                string? cursor = null;
                var cursors = new HashSet<string>();
                stock = 0;
                for (var pageNumber = 0; pageNumber < 100; pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = await _source.SearchAsync(name, false, cursor, cancellationToken).ConfigureAwait(false);
                    foreach (var item in page.Items.Where(i => i.Name == name))
                    {
                        stock += Math.Max(0, item.Count);
                        listingCount++;
                        lowest = lowest.HasValue ? Math.Min(lowest.Value, item.Price) : item.Price;
                    }
                    cursor = page.NextCursor;
                    if (string.IsNullOrEmpty(cursor)) { stockComplete = true; break; }
                    if (!cursors.Add(cursor)) break;
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { if (listingCount == 0) stock = null; errors.Add("현재 매물 조회: " + FriendlyError(ex)); }
            try
            {
                string? cursor = null;
                var cursors = new HashSet<string>();
                for (var pageNumber = 0; pageNumber < 100; pageNumber++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var page = await _source.GetHistoryAsync(name, cursor, cancellationToken).ConfigureAwait(false);
                    MergeTrades(_history, (page.Trades ?? new()).Where(t => t.Name == name));
                    cursor = page.NextCursor;
                    if (string.IsNullOrEmpty(cursor)) { historyComplete = true; break; }
                    if (!cursors.Add(cursor)) break;
                    await Task.Delay(250, cancellationToken).ConfigureAwait(false);
                }
                Directory.CreateDirectory(Path.GetDirectoryName(_historyPath)!);
                var temporary = _historyPath + ".tmp";
                await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(_history.Values), cancellationToken).ConfigureAwait(false);
                File.Move(temporary, _historyPath, true);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { errors.Add("거래 기록 조회/저장: " + FriendlyError(ex)); }
            cancellationToken.ThrowIfCancellationRequested();
            var result = new AuctionInsights
            {
                Name = name, CheckedAt = DateTimeOffset.UtcNow, Stock = stock, ListingCount = listingCount,
                LowestPrice = lowest, StockComplete = stockComplete, HistoryComplete = historyComplete,
                Trades = _history.Values.Where(t => t.Name == name).OrderByDescending(t => t.BoughtAt).ToArray(), Error = string.Join("\n", errors)
            };
            _cache[name] = result;
            return result;
        }
        finally { _gate.Release(); }
    }
    private static string FriendlyError(Exception ex) => ex is System.Net.Http.HttpRequestException ? "인터넷 연결을 확인하세요." : ex.Message;
}

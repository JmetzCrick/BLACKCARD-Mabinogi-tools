using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using BuffAssistant.Services;

internal static class AuctionFeatureTests
{
    public static void Run(Action<bool, string> check)
    {
        var nullOptions = JsonSerializer.Deserialize<AuctionListing>("{\"item_name\":\"검\",\"item_option\":null}");
        check(nullOptions.Details == "상세 옵션 없음", "Null item options no longer crash selection (actual Windows crash regression)");
        var unusualOptions = JsonSerializer.Deserialize<AuctionListing>("{\"item_option\":[null,\"none\",{\"option_type\":\"세공\",\"option_value\":null}]}");
        check(unusualOptions.Details.Contains("세공"), "Unexpected option entries are ignored safely");
        check(FastPingService.IsAppliedValue(1) && !FastPingService.IsAppliedValue(0) && !FastPingService.IsAppliedValue(null), "FastPing status matches supplied installer registry check");
        check(AuctionService.BuildHistoryUri("롱 소드", "a&b").AbsolutePath.EndsWith("/auction/history") && AuctionService.BuildHistoryUri("롱 소드", "a&b").Query.Contains("a%26b"), "History endpoint and pagination cursor encoded");
        var path = Path.Combine(AppContext.BaseDirectory, "test-history-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var source = new FakeAuctionSource();
            var insights = new AuctionInsightsService(source, path);
            var report = insights.LoadAsync("테스트 검", CancellationToken.None).GetAwaiter().GetResult();
            check(report.Stock == 12 && report.ListingCount == 2 && report.StockComplete && report.LowestPrice == 100, "Current stock sums item quantities across all listing pages");
            check(report.Trades.Count == 2 && report.Trades.Sum(t => t.Count) == 5 && report.HistoryComplete, "Trade pagination deduplicates repeated buy IDs");
            check(report.Report.Contains("개당 200 골드") && report.Report.Contains("최근 1시간") && report.Report.Contains("프로그램에서 확인한 기록"), "Latest sale and observed daily counts show API coverage");
            var calls = source.Calls;
            insights.LoadAsync("테스트 검", CancellationToken.None).GetAwaiter().GetResult();
            check(source.Calls == calls, "Repeated selections reuse fresh item summary instead of flooding API");
            source.EmptyHistory = true;
            var restored = new AuctionInsightsService(source, path).LoadAsync("테스트 검", CancellationToken.None).GetAwaiter().GetResult();
            check(restored.Trades.Count == 2, "Observed previous-day trades survive restart and API window expiry");
            var old = new Dictionary<string, AuctionTrade> { ["expired"] = new() { Id = "expired", Name = "테스트 검", Count = 1, BoughtAt = DateTimeOffset.UtcNow.AddDays(-31) } };
            AuctionInsightsService.MergeTrades(old, report.Trades.Concat(report.Trades));
            check(old.Count == 2 && !old.ContainsKey("expired"), "Trade cache removes expired records and avoids double counting");
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            try { insights.LoadAsync("다른 검", canceled.Token).GetAwaiter().GetResult(); check(false, "Canceled selection must stop"); }
            catch (OperationCanceledException) { check(true, "Canceled selection cannot replace newer item details"); }
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}

internal sealed class FakeAuctionSource : IAuctionDataSource
{
    public int Calls;
    public bool EmptyHistory;
    public async Task<AuctionPage> SearchAsync(string query, bool keywords, string cursor, CancellationToken cancellationToken)
    {
        Calls++;
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        return new AuctionPage
        {
            Items = new() { new AuctionListing { Name = query, Count = cursor is null ? 5 : 7, Price = cursor is null ? 200 : 100, Options = null } },
            NextCursor = cursor is null ? "stock-next" : null
        };
    }
    public async Task<AuctionHistoryPage> GetHistoryAsync(string name, string cursor, CancellationToken cancellationToken)
    {
        Calls++;
        await Task.Delay(15, cancellationToken).ConfigureAwait(false);
        if (EmptyHistory) return new AuctionHistoryPage { Trades = null };
        var old = new AuctionTrade { Name = name, Id = "old", Count = 3, Price = 100, BoughtAt = DateTimeOffset.Now.AddDays(-1) };
        var latest = new AuctionTrade { Name = name, Id = "new", Count = 2, Price = 200, BoughtAt = DateTimeOffset.Now.AddMinutes(-10) };
        return new AuctionHistoryPage { Trades = cursor is null ? new() { old } : new() { old, latest }, NextCursor = cursor is null ? "trade-next" : null };
    }
}

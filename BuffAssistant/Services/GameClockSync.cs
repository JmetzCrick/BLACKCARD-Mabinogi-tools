using System.Net.Http;
using System.Diagnostics;

namespace BuffAssistant.Services;

public static class GameClockSync
{
    private static readonly object Sync = new();
    private static DateTimeOffset _serverAnchor;
    private static long _ticks;
    private static bool _synced;
    public static DateTimeOffset Now { get { lock (Sync) return _synced ? _serverAnchor + Stopwatch.GetElapsedTime(_ticks) : DateTimeOffset.UtcNow; } }
    public static async Task SynchronizeAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        using var request = new HttpRequestMessage(HttpMethod.Head, "https://mabinogi.nexon.com/m/main/index.asp");
        request.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true, NoStore = true };
        var started = Stopwatch.GetTimestamp();
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var elapsed = Stopwatch.GetElapsedTime(started);
        if (response.Headers.Date is not { } date || elapsed > TimeSpan.FromSeconds(3)) throw new InvalidOperationException("서버 시각을 확인할 수 없습니다.");
        lock (Sync) { _serverAnchor = date + TimeSpan.FromTicks(elapsed.Ticks / 2); _ticks = Stopwatch.GetTimestamp(); _synced = true; }
    }
}

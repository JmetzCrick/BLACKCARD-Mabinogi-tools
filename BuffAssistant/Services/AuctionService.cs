using System.Net.Http;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace BuffAssistant.Services;

public sealed class AuctionPage
{
    [JsonPropertyName("auction_item")] public List<AuctionListing> Items { get; set; } = new();
    [JsonPropertyName("next_cursor")] public string? NextCursor { get; set; }
}

public sealed class AuctionListing
{
    [JsonPropertyName("item_name")] public string Name { get; set; } = "";
    [JsonPropertyName("item_display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("item_count")] public long Count { get; set; }
    [JsonPropertyName("auction_price_per_unit")] public long Price { get; set; }
    [JsonPropertyName("date_auction_expire")] public DateTimeOffset? Expires { get; set; }
    [JsonPropertyName("item_option")] public List<JsonElement>? Options { get; set; } = new();
    public string Title => string.IsNullOrEmpty(DisplayName) ? Name : DisplayName;
    public string ExpiryText => Expires?.ToLocalTime().ToString("MM/dd HH:mm") ?? "—";
    public string Details => Options is null || Options.Count == 0 ? "상세 옵션 없음" :
        string.Join("\n", Options.Where(o => o.ValueKind == JsonValueKind.Object)
            .Select(o => string.Join(" · ", o.EnumerateObject().Select(p => p.Value.ToString()).Where(v => !string.IsNullOrWhiteSpace(v)))));
}

public sealed class AuctionHistoryPage
{
    [JsonPropertyName("auction_history")] public List<AuctionTrade>? Trades { get; set; } = new();
    [JsonPropertyName("next_cursor")] public string? NextCursor { get; set; }
}
public sealed class AuctionTrade
{
    [JsonPropertyName("item_name")] public string Name { get; set; } = "";
    [JsonPropertyName("item_count")] public long Count { get; set; }
    [JsonPropertyName("auction_price_per_unit")] public long Price { get; set; }
    [JsonPropertyName("date_auction_buy")] public DateTimeOffset? BoughtAt { get; set; }
    [JsonPropertyName("auction_buy_id")] public string? Id { get; set; }
    public string Identity => !string.IsNullOrEmpty(Id) ? Id : $"{Name}|{BoughtAt:O}|{Price}|{Count}";
}

public interface IAuctionDataSource
{
    Task<AuctionPage> SearchAsync(string query, bool keywords, string? cursor, CancellationToken cancellationToken);
    Task<AuctionHistoryPage> GetHistoryAsync(string name, string? cursor, CancellationToken cancellationToken);
}
public sealed class AuctionService : IDisposable, IAuctionDataSource
{
    private static readonly SemaphoreSlim RequestGate = new(1, 1);
    private static DateTimeOffset _nextRequest;
    private readonly HttpClient _client;
    public AuctionService(HttpClient? client = null) => _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    public static Uri BuildUri(string query, bool keywords, string? cursor = null)
    {
        query = NormalizeQuery(query);
        if (string.IsNullOrEmpty(query)) throw new ArgumentException("아이템 이름 또는 키워드를 입력하세요.");
        if (keywords)
        {
            var words = System.Text.RegularExpressions.Regex.Split(query, "[^가-힣a-zA-Z0-9]+").Where(w => w.Length > 0).ToArray();
            if (words.Length is < 1 or > 10) throw new ArgumentException("키워드는 1~10개 입력하세요.");
            query = string.Join(",", words);
        }
        var path = keywords ? "keyword-search?keyword=" : "list?item_name=";
        return new Uri("https://open.api.nexon.com/mabinogi/v1/auction/" + path + Uri.EscapeDataString(query) +
            (string.IsNullOrEmpty(cursor) ? "" : "&cursor=" + Uri.EscapeDataString(cursor)));
    }
    public async Task<AuctionPage> SearchAsync(string query, bool keywords, string? cursor, CancellationToken cancellationToken)
    {
        try { return await GetPageAsync(BuildUri(query, keywords, cursor), cancellationToken); }
        catch (AuctionApiException e) when (!keywords && string.IsNullOrEmpty(cursor) && e.Code == "OPENAPI00004")
        {
            // Exact-name endpoint rejects partial names instead of returning an empty list.
            return await GetPageAsync(BuildUri(query, true), cancellationToken);
        }
    }
    public static string NormalizeQuery(string query) => System.Text.RegularExpressions.Regex.Replace(
        string.Concat(query.Normalize(System.Text.NormalizationForm.FormKC).Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.Format)), @"\s+", " ").Trim();
    public Task<AuctionPage> GetCatalogPageAsync(string? cursor, CancellationToken cancellationToken) =>
        GetPageAsync(new Uri("https://open.api.nexon.com/mabinogi/v1/auction/list" +
            (string.IsNullOrEmpty(cursor) ? "" : "?cursor=" + Uri.EscapeDataString(cursor))), cancellationToken);
    public static Uri BuildHistoryUri(string name, string? cursor = null) =>
        new(BuildUri(name, false, cursor).AbsoluteUri.Replace("/auction/list?", "/auction/history?"));
    public async Task<AuctionHistoryPage> GetHistoryAsync(string name, string? cursor, CancellationToken cancellationToken) =>
        JsonSerializer.Deserialize<AuctionHistoryPage>(await GetJsonAsync(BuildHistoryUri(name, cursor), cancellationToken))
        ?? throw new InvalidOperationException("거래 기록을 읽을 수 없습니다.");
    private async Task<AuctionPage> GetPageAsync(Uri uri, CancellationToken cancellationToken)
    {
        var page = JsonSerializer.Deserialize<AuctionPage>(await GetJsonAsync(uri, cancellationToken))
            ?? throw new InvalidOperationException("검색 결과를 읽을 수 없습니다.");
        page.Items ??= new();
        return page;
    }
    private async Task<string> GetJsonAsync(Uri uri, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
        await RequestGate.WaitAsync(cancellationToken);
        try
        {
        var wait = _nextRequest - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait, cancellationToken);
        _nextRequest = DateTimeOffset.UtcNow.AddMilliseconds(600);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("x-nxopen-api-key", ReadKey());
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            if ((int)response.StatusCode == 429 && attempt < 2) { await Task.Delay(TimeSpan.FromSeconds(attempt + 1), cancellationToken); continue; }
            var code = "";
            try { using var error = JsonDocument.Parse(body); code = error.RootElement.GetProperty("error").GetProperty("name").GetString() ?? ""; } catch (JsonException) { } catch (KeyNotFoundException) { }
            if (code == "OPENAPI00009" && attempt == 0) { await Task.Delay(1000, cancellationToken); continue; }
            var reason = (int)response.StatusCode switch
            {
                403 => "API 키의 유효기간 또는 마비노기 접근 권한을 확인하세요.",
                429 => "API 요청 한도를 초과했습니다. 잠시 후 다시 검색하세요.",
                400 when code == "OPENAPI00005" => "내장 API 키가 만료되었거나 유효하지 않습니다. 최신 배포본으로 업데이트해 주세요.",
                400 when code == "OPENAPI00009" => "넥슨이 경매장 데이터를 준비 중입니다. 잠시 후 다시 조회해 주세요.",
                400 when code == "OPENAPI00010" => "마비노기 점검 중입니다. 점검 종료 후 다시 조회해 주세요.",
                503 => "넥슨 Open API 점검 중입니다. 점검 종료 후 다시 조회해 주세요.",
                400 when !string.IsNullOrEmpty(uri.Query) && uri.Query.Contains("cursor=") => "검색 목록의 유효시간이 지났습니다. 검색 버튼으로 다시 조회해 주세요.",
                400 => "해당 검색어를 조회할 수 없습니다. 추천 아이템을 선택하거나 키워드 검색을 이용해 주세요.",
                _ => "넥슨 서버에 연결할 수 없습니다. 잠시 후 다시 검색하세요."
            };
            throw new AuctionApiException(code, $"{reason} (HTTP {(int)response.StatusCode} · {code})");
        }
        return body;
        }
        finally { RequestGate.Release(); }
        }
    }
    private static string ReadKey() => BundledAuctionKey.Read();
    public static void SaveKey(string key)
    {
        key = key.Trim();
        if (key.Length < 20 || key.Any(char.IsWhiteSpace)) throw new ArgumentException("올바른 넥슨 Open API 키를 입력하세요.");
        var bytes = System.Text.Encoding.UTF8.GetBytes(key);
        var input = new Blob { Length = bytes.Length, Data = Marshal.AllocHGlobal(bytes.Length) };
        try
        {
            Marshal.Copy(bytes, 0, input.Data, bytes.Length);
            if (!CryptProtectData(ref input, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, out var output)) throw new Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                var encrypted = new byte[output.Length];
                Marshal.Copy(output.Data, encrypted, 0, encrypted.Length);
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BuffAssistant");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, "auction-key.bin"), encrypted);
            }
            finally { LocalFree(output.Data); }
        }
        finally { Array.Clear(bytes); Marshal.FreeHGlobal(input.Data); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Blob { public int Length; public IntPtr Data; }
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)] private static extern bool CryptProtectData(ref Blob input, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr pointer);
    public void Dispose() => _client.Dispose();
}

public sealed class AuctionApiException(string code, string message) : InvalidOperationException(message)
{
    public string Code { get; } = code;
}


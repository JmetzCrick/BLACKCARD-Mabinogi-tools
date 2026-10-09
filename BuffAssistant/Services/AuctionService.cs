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
    private readonly HttpClient _client;
    public AuctionService(HttpClient? client = null) => _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
    public static Uri BuildUri(string query, bool keywords, string? cursor = null)
    {
        query = query.Trim();
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
        return await GetPageAsync(BuildUri(query, keywords, cursor), cancellationToken);
    }
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
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("x-nxopen-api-key", ReadKey());
        using var response = await _client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var reason = (int)response.StatusCode switch
            {
                403 => "API 키의 유효기간 또는 마비노기 접근 권한을 확인하세요.",
                429 => "API 요청 한도를 초과했습니다. 잠시 후 다시 검색하세요.",
                400 => "검색 조건을 확인하세요.",
                _ => "넥슨 서버에 연결할 수 없습니다. 잠시 후 다시 검색하세요."
            };
            throw new InvalidOperationException($"{reason} (HTTP {(int)response.StatusCode})");
        }
        return body;
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


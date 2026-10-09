using System.IO;
using System.Net.Http;
using System.Text.Json;

namespace BuffAssistant.Services;

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string Notes { get; set; } = "";
    public string Sha256 { get; set; } = "";
}
public sealed record UpdateCheckResult(UpdateInfo Info, bool Available, string? DownloadTarget);

public sealed class UpdateService : IDisposable
{
    public static readonly string DisplayVersion = typeof(UpdateService).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
        .Cast<System.Reflection.AssemblyInformationalVersionAttribute>().First().InformationalVersion.Split('+')[0];
    private readonly HttpClient _client;
    public UpdateService(HttpClient? client = null)
    {
        _client = client ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("BlackCardHelper/" + typeof(UpdateService).Assembly.GetName().Version);
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    public static string? GitHubFeed
    {
        get
        {
            var file = Path.Combine(AppContext.BaseDirectory, "github-update.json");
            if (!File.Exists(file)) return null;
            using var config = JsonDocument.Parse(File.ReadAllText(file));
            var repo = config.RootElement.GetProperty("repository").GetString();
            if (repo is null || !System.Text.RegularExpressions.Regex.IsMatch(repo, @"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")) throw new InvalidOperationException("GitHub 저장소 설정을 확인하세요.");
            return "https://api.github.com/repos/" + repo + "/releases?per_page=30";
        }
    }
    public static string SuggestedFeedPath
    {
        get
        {
            var adjacent = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "UpdateShare", "latest.json"));
            return File.Exists(adjacent) ? adjacent : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "UpdateShare", "latest.json"));
        }
    }
    public static bool IsNewer(string text)
    {
        var version = text.Trim();
        var beta = version.StartsWith("BETA ", StringComparison.OrdinalIgnoreCase) || version.Contains("-beta", StringComparison.OrdinalIgnoreCase);
        version = version.Replace("BETA ", "", StringComparison.OrdinalIgnoreCase).TrimStart('v', 'V');
        version = version.Split('-')[0];
        if (!System.Version.TryParse(version, out var parsed)) throw new InvalidOperationException("버전 정보 파일의 버전 번호를 확인하세요.");
        var normalized = new System.Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
        var current = typeof(UpdateService).Assembly.GetName().Version!;
        return normalized > current || (normalized == current && !beta && DisplayVersion.StartsWith("BETA", StringComparison.OrdinalIgnoreCase));
    }
    public static string ConfiguredFeed(string fallback)
    {
        var config = Path.Combine(AppContext.BaseDirectory, "update-source.json");
        if (!File.Exists(config)) return fallback;
        using var document = JsonDocument.Parse(File.ReadAllText(config));
        if (!document.RootElement.TryGetProperty("feedPath", out var property)) return fallback;
        var path = property.GetString();
        if (string.IsNullOrWhiteSpace(path)) return fallback;
        return path.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));
    }
    public static string ResolveFileFeed(string path)
    {
        path = path.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("업데이트 공유 폴더 또는 버전 정보 파일을 지정하세요.");
        return path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ? path : Path.Combine(path, "latest.json");
    }
    public async Task<UpdateCheckResult> CheckAsync(string feed, CancellationToken cancellationToken)
    {
        if (feed.StartsWith("https://api.github.com/repos/", StringComparison.OrdinalIgnoreCase)) return await CheckGitHubAsync(feed, cancellationToken).ConfigureAwait(false);
        var remote = Uri.TryCreate(feed.Trim(), UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
        string json;
        string? filePath = null;
        if (remote)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 65536) throw new InvalidOperationException("버전 정보 파일이 너무 큽니다.");
            json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            if (feed.Contains("://")) throw new ArgumentException("공유 폴더 경로 또는 HTTPS 주소를 사용하세요.");
            filePath = Path.GetFullPath(ResolveFileFeed(feed));
            json = await Task.Run(async () =>
            {
                var file = new FileInfo(filePath);
                if (file.Length > 65536) throw new InvalidOperationException("버전 정보 파일이 너무 큽니다.");
                return await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
        }
        if (json.Length > 65536) throw new InvalidOperationException("버전 정보 파일이 너무 큽니다.");
        var info = JsonSerializer.Deserialize<UpdateInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("버전 정보 파일을 읽을 수 없습니다.");
        var available = IsNewer(info.Version);
        string? target = null;
        if (!string.IsNullOrWhiteSpace(info.DownloadUrl))
        {
            if (Uri.TryCreate(info.DownloadUrl, UriKind.Absolute, out var download) && download.Scheme == Uri.UriSchemeHttps) target = download.AbsoluteUri;
            else if (!remote && !info.DownloadUrl.Contains("://") && string.Equals(Path.GetExtension(info.DownloadUrl), ".zip", StringComparison.OrdinalIgnoreCase))
                target = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(filePath!)!, info.DownloadUrl));
            else throw new InvalidOperationException("업데이트 파일은 ZIP 경로 또는 HTTPS 주소로 지정하세요.");
        }
        return new(info, available, target);
    }
    private async Task<UpdateCheckResult> CheckGitHubAsync(string feed, CancellationToken token)
    {
        using var response = await _client.GetAsync(feed, token).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) throw new InvalidOperationException("공개 GitHub 저장소와 Releases 배포 상태를 확인하세요.");
        if ((int)response.StatusCode is 403 or 429) throw new InvalidOperationException("GitHub 요청 한도에 도달했습니다. 잠시 후 확인하세요.");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
        if (json.Length > 2 * 1024 * 1024) throw new InvalidOperationException("GitHub 버전 정보가 너무 큽니다.");
        return ParseGitHubReleases(json);
    }
    public static UpdateCheckResult ParseGitHubReleases(string json)
    {
        using var document = JsonDocument.Parse(json);
        UpdateCheckResult? newest = null;
        System.Version? newestVersion = null;
        bool newestStable = false;
        foreach (var release in document.RootElement.EnumerateArray())
        {
            if (release.TryGetProperty("draft", out var draft) && draft.GetBoolean()) continue;
            var tag = release.GetProperty("tag_name").GetString() ?? "";
            var number = tag.Replace("BETA ", "", StringComparison.OrdinalIgnoreCase).TrimStart('v', 'V').Split('-')[0];
            if (!System.Version.TryParse(number, out var parsed)) continue;
            var version = new System.Version(parsed.Major, parsed.Minor, Math.Max(0, parsed.Build), Math.Max(0, parsed.Revision));
            var beta = tag.Contains("beta", StringComparison.OrdinalIgnoreCase) || (release.TryGetProperty("prerelease", out var pre) && pre.GetBoolean());
            var assets = release.GetProperty("assets").EnumerateArray().Where(asset =>
                asset.GetProperty("name").GetString() is string name && name.StartsWith("BlackCardHelper-", StringComparison.OrdinalIgnoreCase) && name.EndsWith("-win-x64.zip", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (assets.Length != 1) continue;
            var asset = assets[0];
            var target = asset.GetProperty("browser_download_url").GetString();
            if (!Uri.TryCreate(target, UriKind.Absolute, out var url) || url.Scheme != "https" || url.Host != "github.com") throw new InvalidOperationException("GitHub 배포 파일 주소를 확인하세요.");
            if (newestVersion is not null && (version < newestVersion || (version == newestVersion && (newestStable || beta)))) continue;
            var digest = asset.TryGetProperty("digest", out var digestProperty) ? digestProperty.GetString() : null;
            if (digest is null || !System.Text.RegularExpressions.Regex.IsMatch(digest, @"^sha256:[a-fA-F0-9]{64}$")) throw new InvalidOperationException("GitHub 배포 ZIP의 SHA256 정보가 없습니다. 파일을 다시 업로드하세요.");
            var display = beta ? "BETA " + number : number;
            newest = new(new UpdateInfo { Version = display, DownloadUrl = target!, Sha256 = digest[7..], Notes = release.TryGetProperty("body", out var body) ? body.GetString() ?? "" : "" }, IsNewer(display), target);
            newestVersion = version;
            newestStable = !beta;
        }
        return newest ?? throw new InvalidOperationException("GitHub Releases에 Windows x64 배포 ZIP이 아직 없습니다.");
    }
    public void Dispose() => _client.Dispose();
}

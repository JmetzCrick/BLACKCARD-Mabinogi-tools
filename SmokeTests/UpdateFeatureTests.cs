using System;
using System.IO;
using System.Threading;
using System.Text.Json;
using BuffAssistant.Services;

internal static class UpdateFeatureTests
{
    public static void Run(Action<bool, string> check)
    {
        var current = typeof(UpdateService).Assembly.GetName().Version!;
        var next = $"{current.Major}.{current.Minor}.{current.Build + 1}";
        var stable = $"{current.Major}.{current.Minor}.{current.Build}";
        check(!UpdateService.IsNewer(UpdateService.DisplayVersion) && UpdateService.IsNewer("BETA " + next) && UpdateService.IsNewer(stable), "Beta version comparison distinguishes newer beta and stable release");
        check(!UpdateService.IsNewer("BETA 0.0") && UpdateService.IsNewer("v" + next + "-beta.1"), "Version comparison rejects downgrade and accepts newer patch");
        var githubJson = JsonSerializer.Serialize(new[] {
            new { tag_name = "v" + next + "-beta.1", draft = false, prerelease = true, body = "GitHub 업데이트", assets = new[] { new { name = "BlackCardHelper-" + next + "-win-x64.zip", browser_download_url = "https://github.com/test/repo/releases/download/v" + next + "/package.zip", digest = "sha256:" + new string('a', 64) } } },
            new { tag_name = "v99.0.0", draft = true, prerelease = false, body = "draft", assets = new[] { new { name = "BlackCardHelper-99.0.0-win-x64.zip", browser_download_url = "https://github.com/test/repo/releases/download/v99/package.zip", digest = "sha256:" + new string('b', 64) } } }
        });
        var github = UpdateService.ParseGitHubReleases(githubJson);
        check(github.Available && github.Info.Version == "BETA " + next && github.Info.Sha256 == new string('a', 64), "GitHub beta releases are detected and drafts excluded with SHA256");
        try { UpdateService.ParseGitHubReleases(githubJson.Replace("https://github.com/", "http://evil.example/")); check(false, "Unsafe release must fail"); }
        catch (InvalidOperationException) { check(true, "GitHub updater rejects non-GitHub downloads"); }
        try { UpdateService.ParseGitHubReleases("[]"); check(false, "No releases must fail"); }
        catch (InvalidOperationException) { check(true, "Missing GitHub release does not falsely claim current version"); }
        check(UpdateService.ResolveFileFeed(@"\\PC\black.card") == @"\\PC\black.card\latest.json", "Network share paths accept dotted folder names");
        var folder = Path.Combine(AppContext.BaseDirectory, "update-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "latest.json");
        try
        {
            using var service = new UpdateService();
            File.WriteAllText(path, JsonSerializer.Serialize(new { version = "BETA " + next, downloadUrl = "new.zip", notes = "새 버전" }));
            var result = service.CheckAsync(folder, CancellationToken.None).GetAwaiter().GetResult();
            check(result.Available && result.DownloadTarget == Path.Combine(folder, "new.zip"), "Shared file manifest resolves relative update archive");
            File.WriteAllText(path, "{\"version\":\"BETA 0.1\"}");
            check(!service.CheckAsync(path, CancellationToken.None).GetAwaiter().GetResult().Available, "Matching shared version reports current release");
            File.WriteAllText(path, "{invalid json");
            try { service.CheckAsync(path, CancellationToken.None).GetAwaiter().GetResult(); check(false, "Invalid manifest must fail"); }
            catch (JsonException) { check(true, "Malformed update manifest does not falsely claim latest version"); }
            File.Delete(path);
            try { service.CheckAsync(path, CancellationToken.None).GetAwaiter().GetResult(); check(false, "Missing manifest must fail"); }
            catch (IOException) { check(true, "Missing shared manifest reports unavailable rather than up-to-date"); }
        }
        finally { if (File.Exists(path)) File.Delete(path); Directory.Delete(folder); }
    }
}

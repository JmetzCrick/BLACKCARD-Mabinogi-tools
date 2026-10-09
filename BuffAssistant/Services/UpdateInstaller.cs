using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace BuffAssistant.Services;

public sealed record UpdatePlan(int ProcessId, string Target, string Stage, string Backup, string Executable);
public static class UpdateInstaller
{
    public static string SafeDestination(string root, string entryName)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(normalizedRoot, entryName.Replace('/', Path.DirectorySeparatorChar)));
        if (!target.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("업데이트 ZIP에 허용되지 않은 경로가 있습니다.");
        return target;
    }
    public static async Task<UpdatePlan> PrepareAsync(UpdateCheckResult update, string installDirectory, string? workDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(update.DownloadTarget)) throw new InvalidOperationException("배포 정보에 업데이트 ZIP 파일이 지정되지 않았습니다.");
        var work = workDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BuffAssistant", "Updates", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        var zipPath = Path.Combine(work, "package.zip");
        if (update.DownloadTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var response = await client.GetAsync(update.DownloadTarget, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            await using var output = File.Create(zipPath);
            await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }
        else await Task.Run(() => File.Copy(update.DownloadTarget, zipPath, true), cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(update.Info.Sha256))
        {
            await using var stream = File.OpenRead(zipPath);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false));
            if (!actual.Equals(update.Info.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("업데이트 파일 검증에 실패했습니다. 배포 파일을 확인하세요.");
        }
        var stage = Path.Combine(work, "files");
        Directory.CreateDirectory(stage);
        await Task.Run(() =>
        {
            using var archive = ZipFile.OpenRead(zipPath);
            if (archive.Entries.Count > 10000 || archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new InvalidOperationException("업데이트 패키지가 너무 큽니다.");
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var destination = SafeDestination(stage, entry.FullName);
                if (entry.FullName.EndsWith('/')) { Directory.CreateDirectory(destination); continue; }
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, false);
            }
        }, cancellationToken).ConfigureAwait(false);
        const string exe = "블랙카드 도우미.exe";
        foreach (var file in new[] { exe, "블랙카드 도우미.dll", "블랙카드 도우미.runtimeconfig.json" })
            if (!File.Exists(Path.Combine(stage, file))) throw new InvalidOperationException("업데이트 ZIP에 프로그램 실행 파일이 없습니다.");
        return new UpdatePlan(Environment.ProcessId, Path.GetFullPath(installDirectory).TrimEnd(Path.DirectorySeparatorChar), stage, Path.Combine(work, "backup"), exe);
    }
    public static void Start(UpdatePlan plan)
    {
        var helper = Path.Combine(AppContext.BaseDirectory, "Assets", "Updates", "ApplyUpdate.ps1");
        if (!File.Exists(helper)) throw new FileNotFoundException("업데이트 적용 도구가 없습니다.");
        var helperCopy = Path.Combine(Path.GetDirectoryName(plan.Stage)!, "ApplyUpdate.ps1");
        File.Copy(helper, helperCopy, true);
        var planPath = Path.Combine(Path.GetDirectoryName(plan.Stage)!, "plan.json");
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan));
        var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"))
        { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", helperCopy, "-PlanPath", planPath }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("업데이트 도구를 실행할 수 없습니다.");
    }
}

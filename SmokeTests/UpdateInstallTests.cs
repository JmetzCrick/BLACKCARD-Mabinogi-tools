using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading;
using System.Diagnostics;
using BuffAssistant.Services;

internal static class UpdateInstallTests
{
    public static void Run(Action<bool, string> check)
    {
        var root = Path.Combine(AppContext.BaseDirectory, "installer-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, "installed");
        var stage = Path.Combine(root, "payload");
        Directory.CreateDirectory(target);
        Directory.CreateDirectory(stage);
        try
        {
            File.WriteAllText(Path.Combine(target, "블랙카드 도우미.exe"), "old program");
            File.WriteAllText(Path.Combine(target, "auction-key.bin"), "private key");
            File.WriteAllText(Path.Combine(stage, "블랙카드 도우미.exe"), "new program");
            File.WriteAllText(Path.Combine(stage, "블랙카드 도우미.dll"), "new dll");
            File.WriteAllText(Path.Combine(stage, "블랙카드 도우미.runtimeconfig.json"), "{}");
            File.WriteAllText(Path.Combine(stage, "auction-key.bin"), "publisher key");
            var zip = Path.Combine(root, "new.zip");
            ZipFile.CreateFromDirectory(stage, zip);
            var update = new UpdateCheckResult(new UpdateInfo { Version = "BETA 0.2" }, true, zip);
            var plan = UpdateInstaller.PrepareAsync(update, target, Path.Combine(root, "prepared"), CancellationToken.None).GetAwaiter().GetResult();
            check(File.ReadAllText(Path.Combine(target, "블랙카드 도우미.exe")) == "old program", "Preparing update does not overwrite running installation");
            plan = plan with { ProcessId = int.MaxValue };
            var planPath = Path.Combine(root, "plan.json");
            File.WriteAllText(planPath, JsonSerializer.Serialize(plan));
            var start = new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var arg in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(AppContext.BaseDirectory, "Assets", "Updates", "ApplyUpdate.ps1"), "-PlanPath", planPath, "-NoRestart" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (!process.WaitForExit(15000)) { process.Kill(); throw new Exception("Updater fixture timed out"); }
            check(process.ExitCode == 0, "One-click helper installs fixture successfully: " + process.StandardError.ReadToEnd());
            check(File.ReadAllText(Path.Combine(target, "블랙카드 도우미.exe")) == "new program" && File.ReadAllText(Path.Combine(plan.Backup, "블랙카드 도우미.exe")) == "old program", "Updater replaces program and retains previous files for recovery");
            check(File.ReadAllText(Path.Combine(target, "auction-key.bin")) == "private key", "Updater preserves personal encrypted API key");
            File.WriteAllText(Path.Combine(target, "00-first.txt"), "original");
            File.WriteAllText(Path.Combine(plan.Stage, "00-first.txt"), "replacement");
            Directory.CreateDirectory(Path.Combine(target, "zzz-conflict.txt"));
            File.WriteAllText(Path.Combine(plan.Stage, "zzz-conflict.txt"), "conflict");
            using (var rollback = Process.Start(start))
            {
                if (!rollback.WaitForExit(15000)) { rollback.Kill(); throw new Exception("Rollback fixture timed out"); }
                check(rollback.ExitCode == 1 && File.ReadAllText(Path.Combine(target, "00-first.txt")) == "original", "Failed installation restores files already replaced");
            }
            try { UpdateInstaller.SafeDestination(stage, "../escape.dll"); check(false, "ZIP traversal must fail"); }
            catch (InvalidOperationException) { check(true, "Updater rejects ZIP entries escaping staging folder"); }
            var badZip = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(badZip, ZipArchiveMode.Create)) { using var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open()); writer.Write("not an application"); }
            try { UpdateInstaller.PrepareAsync(update with { DownloadTarget = badZip }, target, Path.Combine(root, "bad-stage"), CancellationToken.None).GetAwaiter().GetResult(); check(false, "Missing executable must fail"); }
            catch (InvalidOperationException) { check(true, "Updater rejects incomplete package before changing installed files"); }
        }
        finally
        {
            // Fixture deletion stays under the test executable output directory.
            if (Path.GetFullPath(root).StartsWith(Path.GetFullPath(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }
}

using BuffAssistant.Services;
using System.IO;

using var updates = new UpdateService();
var feed = UpdateService.GitHubFeed ?? throw new Exception("Missing GitHub configuration");
var release = await updates.CheckAsync(feed, CancellationToken.None);
if (release.Available || release.Info.Version != UpdateService.DisplayVersion) throw new Exception("Latest GitHub version does not match published program");
Console.WriteLine("PASS: Real program updater reads GitHub and identifies the current release: " + release.Info.Version);
var folder = Path.Combine(AppContext.BaseDirectory, "GitHubPackageVerification");
var plan = await UpdateInstaller.PrepareAsync(release, Path.Combine(folder, "installation"), Path.Combine(folder, "staging"), CancellationToken.None);
var assembly = System.Reflection.Assembly.LoadFile(Path.Combine(plan.Stage, "블랙카드 도우미.dll"));
if (!assembly.GetManifestResourceNames().Contains("BlackCardHelper.AuctionKey")) throw new Exception("Embedded auction key missing from GitHub ZIP");
if (!File.Exists(Path.Combine(plan.Stage, "Assets", "Alerts", "Gathering.wav"))) throw new Exception("Gathering chime missing");
if (Directory.GetFiles(plan.Stage, "*traineddata", SearchOption.AllDirectories).Length > 0 || File.Exists(Path.Combine(plan.Stage, "Tesseract.dll"))) throw new Exception("OCR files in GitHub release");
Console.WriteLine("PASS: GitHub ZIP downloads, passes program SHA256 validation and extracts without OCR files");


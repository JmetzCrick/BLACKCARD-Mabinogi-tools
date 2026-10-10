using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BuffAssistant;
using BuffAssistant.Services;

internal static class Program
{
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        Console.WriteLine("PASS: " + name);
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            if (args.Length > 0 && args[0] == "--clock-sync-test")
            {
                GameClockSync.SynchronizeAsync().GetAwaiter().GetResult();
                var start = GameClockSync.Now;
                Thread.Sleep(100);
                Check(GameClockSync.Now > start, "Official HTTPS time anchor advances independently of system clock changes");
                return 0;
            }
            if(args.Length > 0 && args[0] == "--music-files-test")
            {
                foreach(var file in MusicCatalog.StartupPlaylist(Array.Empty<string>(),Path.Combine(AppContext.BaseDirectory,"Assets","Music")))
                {
                    using var reader = new NAudio.Wave.AudioFileReader(file);
                    Check(reader.TotalTime > TimeSpan.FromSeconds(10), "Bundled track decodes: " + MusicCatalog.DisplayName(file));
                }
                return 0;
            }
            if(args.Length > 0 && args[0] == "--tray-test")
            {
                var trayApp=new App(); trayApp.InitializeComponent();
                var window=new MainWindow(autoPlayMusic:false);
                typeof(MainWindow).GetMethod("InitializeTray",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);
                window.Show(); window.HideToTray();
                var tray=(System.Windows.Forms.NotifyIcon)typeof(MainWindow).GetField("_tray",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(window);
                Check(!window.IsVisible && !window.ShowInTaskbar && tray.Visible,"Background conversion hides the window while keeping the character tray icon active");
                typeof(MainWindow).GetMethod("RestoreFromTray",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(window,null);
                Check(window.IsVisible && window.ShowInTaskbar,"Tray restore brings the functional window back without restarting services");
                window.Close();
                return 0;
            }
            if (args.Length > 0 && args[0] == "--auction-ui-test")
            {
                var liveApp = new App(); liveApp.InitializeComponent();
                var live = new MainWindow(autoPlayMusic:false);
                var frame = new System.Windows.Threading.DispatcherFrame();
                Exception failure = null;
                live.Dispatcher.BeginInvoke(new Action(async () => {
                    try {
                        foreach (var sample in new[] { ("희미한 에너지 조각", 0), ("희미한에너지조각", 0), ("희미한 에너지 조각", 1) }) {
                            ((TextBox)live.FindName("AuctionQueryBox")).Text = sample.Item1;
                            ((ComboBox)live.FindName("AuctionModeBox")).SelectedIndex = sample.Item2;
                            await (System.Threading.Tasks.Task)typeof(MainWindow).GetMethod("SearchAuctionAsync", BindingFlags.NonPublic|BindingFlags.Instance).Invoke(live,new object[]{false});
                            var count = ((DataGrid)live.FindName("AuctionResultsGrid")).Items.Count;
                            Check(count > 0,"Actual WPF search succeeds: " + sample.Item1 + " mode " + sample.Item2 + " listings " + count);
                        }
                    } catch(Exception e) { failure=e; } finally { frame.Continue=false; }
                }));
                System.Windows.Threading.Dispatcher.PushFrame(frame); live.Close();
                if(failure != null) throw failure;
                return 0;
            }
            if (args.Length > 0 && args[0] == "--updater-fixture") { UpdateInstallTests.Run(Check); return 0; }
            if (args.Length > 0 && args[0] == "--auction-test")
            {
                using var auction = new AuctionService();
                foreach (var keywords in new[] { false, true }) Check(auction.SearchAsync("롱 소드", keywords, null, CancellationToken.None).GetAwaiter().GetResult().Items.Count > 0, "Encrypted shared key retrieves live auction listings");
                return 0;
            }
            AudioVolumeTests.Run(Check);
            Check(ErinWeekday.At(new DateTimeOffset(2026,10,11,14,59,59,TimeSpan.Zero)).Label == "임볼릭 · 일", "Weekday uses KST before midnight");
            Check(ErinWeekday.At(new DateTimeOffset(2026,10,11,15,0,0,TimeSpan.Zero)).Label == "알반 에일레르 · 월", "Weekday switches at KST midnight independently of PC timezone");
            CalculatorTests.Run(Check);
            GatheringTests.Run(Check);
            var bundleFolder = Path.Combine(AppContext.BaseDirectory,"Assets","Music");
            var playlist = MusicCatalog.StartupPlaylist(Array.Empty<string>(),bundleFolder);
            Check(playlist.Count == 6 && playlist.Any(p=>Path.GetFileName(p)=="Pola.mp3"), "Six supplied music tracks including Pola are bundled");
            var firstTracks = new System.Collections.Generic.HashSet<string>();
            for(var attempt=0;attempt<30;attempt++)
            {
                playlist=MusicCatalog.StartupPlaylist(playlist,bundleFolder);
                Check(playlist.Count==6,"Restart does not duplicate bundled playlist");
                firstTracks.Add(playlist[0]);
            }
            Check(firstTracks.Count > 1,"Startup song is randomized among the supplied bundled tracks");
            AuctionFeatureTests.Run(Check);
            AuctionRequestTests.Run(Check);
            UpdateFeatureTests.Run(Check);
            if (args.Length == 0) AudioPolicyTests.Run(Check);
            var app = new App();
            app.InitializeComponent();
            var main = new MainWindow(autoPlayMusic: false, auctionDataSource: new FakeAuctionSource(), auctionHistoryPath: Path.Combine(AppContext.BaseDirectory, "ui-history.json"));
            Check(typeof(MainWindow).GetField("_ocr", BindingFlags.NonPublic | BindingFlags.Instance) == null && typeof(MainWindow).Assembly.GetType("BuffAssistant.Services.OcrService") == null, "Public build contains no active OCR service or engine initialization");
            foreach (var removed in new[] { "Services.BuffMonitorService", "Services.ScreenCaptureService", "Services.ColorDetectionService", "Services.TextContrast", "RegionPickerWindow", "Models.ScreenRect" })
                Check(typeof(MainWindow).Assembly.GetType("BuffAssistant." + removed) == null, "Removed implementation: " + removed);
            Check(typeof(BuffAssistant.Models.BuffRule).GetMethod("TryTrigger") == null && typeof(BuffAssistant.Models.BuffRule).GetMethod("ResetAlert") == null, "Buff rows contain presentation data only");
            var tab = (Button)main.FindName("BuffTabButton");
            Check(Grid.GetRow(tab) == 1 && Grid.GetColumn(tab) == 1, "Music buff tab is left of the rightmost settings tab");
            Check(((TextBlock)main.FindName("BuffWarningIcon")).Text == "❗", "Music buff tab has small warning emoji");
            typeof(MainWindow).GetMethod("BuffTab_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            var menu = (Grid)main.FindName("BuffMenu");
            var notice = (TextBlock)main.FindName("BuffPolicyNotice");
            Check(!menu.IsEnabled && !menu.IsHitTestVisible && menu.Opacity == 0.22, "Original buff menu remains faded and cannot execute actions");
            Check(notice.Text == "넥슨정책상 해당 기능이 지원되지 않습니다." && notice.VerticalAlignment == VerticalAlignment.Center && notice.HorizontalAlignment == HorizontalAlignment.Center && notice.Foreground.ToString() == Brushes.White.ToString(), "Policy notice is centered and white");
            typeof(MainWindow).GetMethod("Start_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            Check(((TextBlock)main.FindName("StatusText")).Text == notice.Text, "Direct detection invocation is blocked without OCR or audio");
            var root = (FrameworkElement)main.Content;
            root.Measure(new Size(440,680)); root.Arrange(new Rect(0,0,440,680)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(440,680,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using (var file = File.Create("buff-policy-preview.png")) encoder.Save(file);
            CalculatorTests.RunUi(main, Check);
            typeof(MainWindow).GetMethod("GatheringTab_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            Check(((Border)main.FindName("GatheringHost")).Visibility == Visibility.Visible && ((Grid)main.FindName("BuffPanel")).Visibility == Visibility.Collapsed, "Gathering tab opens while music buff stays unavailable");
            var transparency = (Slider)main.FindName("WindowTransparencySlider");
            Check(transparency.IsEnabled && !menu.IsAncestorOf(transparency), "Transparency slider is enabled in the common footer outside the disabled buff menu");
            typeof(MainWindow).GetField("_loadingSettings", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(main, true);
            transparency.Value = 25;
            typeof(MainWindow).GetMethod("ApplyWindowTransparency", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, null);
            Check(((Grid)main.FindName("FeaturePanel")).Opacity == 0.75 && ((Border)main.FindName("TitleBar")).Opacity == 1, "Transparency changes functional content while keeping the title bar opaque");
            transparency.Value = 0;
            typeof(MainWindow).GetMethod("ApplyWindowTransparency", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, null);
            root.Measure(new Size(440,680)); root.Arrange(new Rect(0,0,440,680)); root.UpdateLayout();
            var gatheringBitmap = new RenderTargetBitmap(440,680,96,96,PixelFormats.Pbgra32); gatheringBitmap.Render(root);
            var gatheringEncoder = new PngBitmapEncoder(); gatheringEncoder.Frames.Add(BitmapFrame.Create(gatheringBitmap));
            using (var file = File.Create("gathering-preview.png")) gatheringEncoder.Save(file);
            var clockFrame = (Border)main.FindName("ClockFrame");
            Check(clockFrame.BorderThickness.Left == 1 && clockFrame.Margin.Bottom > 0 && Grid.GetRow(clockFrame) == 0, "Digital clock has matching outline and spacing above title bar");
            typeof(MainWindow).GetMethod("Minimize_Click", BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,new object[]{main,new RoutedEventArgs()});
            Check(main.Height == 106 && clockFrame.Visibility == Visibility.Visible && ((Grid)main.FindName("FeaturePanel")).Visibility == Visibility.Collapsed, "Folding functions retains both digital clock and title bar");
            typeof(MainWindow).GetMethod("Minimize_Click", BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,new object[]{main,new RoutedEventArgs()});
            typeof(MainWindow).GetMethod("SettingsTab_Click", BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,new object[]{main,new RoutedEventArgs()});
            Check(Grid.GetColumn((Button)main.FindName("SettingsTabButton")) == 3 && ((Border)main.FindName("SettingsPanel")).Visibility == Visibility.Visible, "Compact settings tab is rightmost and replaces wide update tab");
            root.Measure(new Size(440,680)); root.Arrange(new Rect(0,0,440,680)); root.UpdateLayout();
            var settingsBitmap = new RenderTargetBitmap(440,680,96,96,PixelFormats.Pbgra32); settingsBitmap.Render(root);
            var settingsEncoder = new PngBitmapEncoder(); settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap));
            using(var file=File.Create("settings-preview.png")) settingsEncoder.Save(file);
            Check(((TextBlock)main.FindName("HeaderVersionText")).Text == UpdateService.DisplayVersion, "Header version follows release metadata");
            main.Close();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}


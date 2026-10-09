using System;
using System.Linq;
using System.Drawing;
using System.Windows;
using BuffAssistant;
using BuffAssistant.Models;
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
            if (args.Length > 0 && args[0] == "--ci")
            {
                DetectionTests.Run(Check);
                CalculatorTests.Run(Check);
                AuctionFeatureTests.Run(Check);
                UpdateFeatureTests.Run(Check);
                return 0;
            }
            if (args.Length > 0 && args[0] == "--updater-fixture") { UpdateInstallTests.Run(Check); return 0; }
            if (args.Length > 0 && args[0] == "--insights-test")
            {
                using var auction = new AuctionService();
                using var deadline = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(45));
                var service = new AuctionInsightsService(auction, System.IO.Path.Combine(AppContext.BaseDirectory, "live-test-history.json"));
                var summary = service.LoadAsync("고급 실크", deadline.Token).GetAwaiter().GetResult();
                Check(summary.Stock > 0 && summary.StockComplete, "Live current stock includes all available listing pages");
                Check(summary.HistoryComplete && summary.Trades.Count > 0 && summary.Error.Length == 0, "Live recent trades decode and persist without errors");
                Console.WriteLine(summary.Report);
                return 0;
            }
            if (args.Length > 0 && args[0] == "--autocomplete-test")
            {
                var catalog = new ItemNameCatalog();
                var names = catalog.Suggest("롱");
                Check(names.Count > 0, "Single-character autocomplete uses bundled real item names");
                using var auction = new AuctionService();
                var selected = names.Contains("롱 소드") ? "롱 소드" : names[0];
                var page = auction.SearchAsync(selected, false, null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                Check(page.Items.Count > 0, "Suggested item name retrieves live auction listings");
                var catalogPage = auction.GetCatalogPageAsync(null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                Check(catalogPage.Items.Count > 0 && !string.IsNullOrEmpty(catalogPage.Items[0].Name), "Live catalog refresh endpoint supplies item names");
                return 0;
            }
            if (args.Length > 0 && args[0] == "--auction-test")
            {
                using var auction = new AuctionService();
                foreach (var keywords in new[] { false, true })
                {
                    var page = auction.SearchAsync("롱 소드", keywords, null, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                    Check(page.Items.Count > 0, keywords ? "Live keyword auction search" : "Live item-name auction search");
                    Check(page.Items.TrueForAll(i => i.Price >= 0 && i.Count > 0 && !string.IsNullOrEmpty(i.Title)), "Live auction fields decoded");
                    if (!string.IsNullOrEmpty(page.NextCursor))
                        Check(auction.SearchAsync("롱 소드", keywords, page.NextCursor, System.Threading.CancellationToken.None).GetAwaiter().GetResult().Items.Count > 0, "Live auction pagination");
                }
                return 0;
            }
            if (args.Length > 0)
            {
                using var diagnosticOcr = new OcrService();
                using var diagnosticImage = new Bitmap(args[0]);
                var diagnosticWords = diagnosticOcr.ReadWords(diagnosticImage);
                foreach (var word in diagnosticWords)
                    Console.WriteLine($"{word.Text}: {word.X},{word.Y} {word.Width}x{word.Height}");
                var diagnosticMatch = BuffMonitorService.FindMatchingWord(diagnosticWords, "전장의 서곡");
                if (diagnosticMatch is not null)
                {
                    var timerRegion = new Rectangle(diagnosticImage.Width / 2, diagnosticMatch.Y - 4, diagnosticImage.Width / 2, diagnosticMatch.Height + 8);
                    Console.WriteLine("Remaining seconds: " + diagnosticOcr.ReadRemainingSeconds(diagnosticImage, timerRegion));
                }
                return 0;
            }
            DetectionTests.Run(Check);
            CalculatorTests.Run(Check);
            AudioPolicyTests.Run(Check);
            AuctionFeatureTests.Run(Check);
            UpdateFeatureTests.Run(Check);
            var suggestions = ItemNameCatalog.MatchNames(new[] { "롱 소드", "배틀 롱 소드", "롱 소드", "활", " 롱 보우 " }, "롱");
            Check(suggestions.Count == 3 && suggestions[0].StartsWith("롱") && suggestions[2] == "배틀 롱 소드", "One-character suggestions match partial names, remove duplicates and rank prefixes");
            Check(ItemNameCatalog.MatchNames(new[] { "롱 소드" }, "롱소").Count == 1, "Autocomplete tolerates omitted spaces");
            Check(ItemNameCatalog.MatchNames(new[] { "롱 소드" }, " ").Count == 0 && ItemNameCatalog.MatchNames(new[] { "롱 소드" }, "없는아이템").Count == 0, "Empty and unmatched autocomplete produce no stale suggestions");
            Check(ItemNameCatalog.MatchNames(new[] { "롱 소드", "롱 보우" }, "롱", 1).Count == 1, "Autocomplete limits dropdown size");
            Check(new ItemNameCatalog().Suggest("검").Count > 0, "Bundled catalog loads real Korean names on first start");
            Check(AuctionService.BuildUri("롱 소드", false).Query.Contains("item_name="), "Auction item-name endpoint");
            Check(Uri.UnescapeDataString(AuctionService.BuildUri("롱 소드", true, "a+b&c").Query).Contains("keyword=롱,소드&cursor=a+b&c"), "Auction keywords and cursor encoded");
            var auctionFixture = System.Text.Json.JsonSerializer.Deserialize<AuctionPage>("{\"auction_item\":[{\"item_name\":\"검\",\"item_count\":2,\"auction_price_per_unit\":5000000000,\"date_auction_expire\":\"2026-10-09T01:00:00Z\",\"item_option\":[{\"option_type\":\"세공\",\"option_value\":\"음악 버프\"}]}],\"next_cursor\":\"next\"}")!;
            Check(auctionFixture.Items[0].Price == 5000000000 && auctionFixture.Items[0].Details.Contains("음악 버프") && auctionFixture.NextCursor == "next", "Auction prices, options and pagination decode");
            var rule = new BuffRule { RequiredConsecutiveDetections = 2 };
            Check(!rule.TryTrigger(true) && rule.TryTrigger(true), "Consecutive detection triggers");
            Check(!rule.TryTrigger(true), "Repeated alert suppressed");
            Check(!rule.TryTrigger(false) && !rule.TryTrigger(true) && rule.TryTrigger(true), "Alert resets after non-red frame");
            rule.ResetAlert();
            Check(!rule.Alerted && rule.ConsecutiveRedFrames == 0, "Manual reset");
            using var bitmap = new Bitmap(10, 10);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Red);
            var colors = new ColorDetectionService();
            Check(colors.GetRedRatio(bitmap, 0, 0, 10, 10) == 1, "Red image detected");
            using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(Color.Gray);
            Check(colors.GetRedRatio(bitmap, -10, -10, 30, 30) == 0, "Gray image excluded and bounds clamped");
            Check(BuffMonitorService.FindMatchingWord(new[] { new OcrWord("전장의", 0, 0, 60, 20), new OcrWord("서곡", 70, 0, 40, 20) }, "전장의 서곡") is { Width: 110 }, "Multiword buff matches with combined bounding box");
            Check(BuffMonitorService.FindMatchingWord(new[] { new OcrWord("전장의", 0, 0, 60, 20), new OcrWord("서곡", 70, 40, 40, 20) }, "전장의 서곡") is null, "Different text lines are not joined");
            using (var ocr = new OcrService())
            using (var sample = new Bitmap(800, 140))
            {
                using (var graphics = Graphics.FromImage(sample))
                using (var font = new Font("Malgun Gothic", 32))
                {
                    graphics.Clear(Color.White);
                    graphics.DrawString("비바체 행진곡 풍년가", font, System.Drawing.Brushes.Black, 20, 30);
                }
                var words = ocr.ReadWords(sample);
                Check(BuffMonitorService.FindMatchingWord(words, "비바체") is not null, "Installed Korean OCR recognizes split buff text");
            }
            var app = new App();
            app.InitializeComponent();
            var gameFont = (System.Windows.Media.FontFamily)app.Resources["GameFont"];
            var gameTypeface = new System.Windows.Media.Typeface(gameFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Check(gameTypeface.TryGetGlyphTypeface(out var bundledTypeface) && bundledTypeface.FamilyNames.Values.Contains("Mabinogi_Classic"), "Provided Mabinogi font resolves from embedded resource without system installation");
            var splash = new SplashWindow();
            Check(splash.Title != null, "Splash XAML loads");
            Check(splash.FindName("CharacterImage") is System.Windows.Controls.Image character && character.Source is System.Windows.Media.Imaging.BitmapSource source && source.PixelWidth > 0, "Loading character image embedded");
            var splashContent = (System.Windows.FrameworkElement)splash.Content;
            splashContent.Measure(new System.Windows.Size(360, 450));
            splashContent.Arrange(new Rect(0, 0, 360, 450));
            splashContent.UpdateLayout();
            var splashPreview = new System.Windows.Media.Imaging.RenderTargetBitmap(360, 450, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            splashPreview.Render(splashContent);
            var splashEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            splashEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(splashPreview));
            using (var file = System.IO.File.Create("loading-preview.png")) splashEncoder.Save(file);
            var uiHistoryPath = System.IO.Path.Combine(AppContext.BaseDirectory, "ui-test-history.json");
            var main = new MainWindow(autoPlayMusic: false, auctionDataSource: new FakeAuctionSource(), auctionHistoryPath: uiHistoryPath);
            CalculatorTests.RunUi(main, Check);
            Check(main.FindName("RulesGrid") is System.Windows.Controls.DataGrid grid && grid.Items.Count == 4, "Main window loads with four default rules");
            Check(main.Width == 440 && main.Height == 584 && main.FindName("LogList") == null, "Compact window with fixed footer and no log panel");
            Check(main.FindName("MusicList") is System.Windows.Controls.ListBox musicList && musicList.Items.Count >= 1 && musicList.SelectedItem != null, "Bundled music selected");
            var musicFile = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Music", "Etain.mp3");
            var alertFile = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Alerts", "음악버프30초.mp3");
            using (var reader = new NAudio.Wave.AudioFileReader(alertFile))
                Check(reader.TotalTime.TotalSeconds > 0, "Default alert MP3 decodes");
            using (var alert = new AudioAlertService())
            {
                alert.Play(alertFile, 0);
                Check(alert.IsPlaying, "Configured alert audio starts (muted)");
                alert.Play(alertFile, 0);
                System.Threading.Thread.Sleep(150);
                Check(alert.IsPlaying, "Previous alert callback does not stop replacement audio");
            }
            foreach (BuffRule defaultRule in ((System.Windows.Controls.DataGrid)main.FindName("RulesGrid")).Items)
                Check(!string.IsNullOrWhiteSpace(defaultRule.AudioFile), "Default alert assigned: " + defaultRule.Name);
            using (var reader = new NAudio.Wave.AudioFileReader(musicFile))
                Check(reader.TotalTime.TotalSeconds > 1, "Bundled MP3 decodes");
            using (var music = new BackgroundMusicService())
            {
                music.SetMuted(true);
                music.PlayPlaylist(new[] { musicFile }, musicFile, 50);
                Check(music.IsPlaying, "Audio playback starts (muted)");
                music.Pause();
                Check(music.IsPaused && !music.IsPlaying, "Audio pauses");
                music.Resume();
                Check(music.IsPlaying, "Audio resumes");
                music.SetVolumePercent(25);
                music.Stop();
                Check(!music.IsPlaying && music.CurrentFile == null, "Audio stops cleanly");
                var shortTrack = System.IO.Path.Combine(AppContext.BaseDirectory, "repeat-test.wav");
                using (var writer = new NAudio.Wave.WaveFileWriter(shortTrack, new NAudio.Wave.WaveFormat(8000, 16, 1)))
                    writer.Write(new byte[1600], 0, 1600);
                music.Repeat = false;
                music.PlayPlaylist(new[] { shortTrack }, shortTrack, 50);
                System.Threading.Thread.Sleep(1200);
                Check(!music.IsPlaying && music.CurrentFile == null, "Playback ends with repeat disabled");
                music.Repeat = true;
                music.PlayPlaylist(new[] { shortTrack }, shortTrack, 50);
                System.Threading.Thread.Sleep(1200);
                Check(music.CurrentFile == shortTrack, "Playback repeats after track ends");
                music.Stop();
                System.IO.File.Delete(shortTrack);
            }
            var content = (System.Windows.FrameworkElement)main.Content;
            content.Measure(new System.Windows.Size(440, 584));
            content.Arrange(new Rect(0, 0, 440, 584));
            content.UpdateLayout();
            main.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
            content.UpdateLayout();
            var preview = new System.Windows.Media.Imaging.RenderTargetBitmap(440, 584, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var visual = new System.Windows.Media.DrawingVisual();
            using (var context = visual.RenderOpen())
            {
                context.DrawRectangle(main.Background, null, new Rect(0, 0, 440, 584));
                context.DrawRectangle(new System.Windows.Media.VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            }
            preview.Render(visual);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));
            using (var file = System.IO.File.Create("design-preview.png")) encoder.Save(file);
            Check(main.Icon is System.Windows.Media.Imaging.BitmapSource icon && icon.PixelWidth > 0, "Character window icon loads");
            using (var exeIcon = System.Drawing.Icon.ExtractAssociatedIcon(System.IO.Path.Combine(AppContext.BaseDirectory, "블랙카드 도우미.exe")))
                Check(exeIcon != null, "Executable icon embedded");
            Check(main.Title == "블랙카드 도우미", "Application renamed");
            ((System.Windows.Controls.Button)main.FindName("AuctionTabButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(((System.Windows.FrameworkElement)main.FindName("AuctionPanel")).Visibility == Visibility.Visible && ((System.Windows.FrameworkElement)main.FindName("BuffPanel")).Visibility == Visibility.Collapsed, "Auction tab opens independently of buff monitoring");
            Check(((System.Windows.Controls.TextBlock)main.FindName("HeaderVersionText")).Text == UpdateService.DisplayVersion, "Main title bar shows beta version");
            Check(System.Windows.Controls.Grid.GetRow((System.Windows.FrameworkElement)main.FindName("ScheduleSiteButton")) == 2 && ((System.Windows.FrameworkElement)main.FindName("ScheduleSiteButton")).Parent == main.FindName("FeaturePanel"), "Schedule site button is a shared footer outside individual tabs");
            var opacitySlider = (System.Windows.Controls.Slider)main.FindName("WindowTransparencySlider");
            typeof(MainWindow).GetField("_loadingSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(main, true);
            opacitySlider.Value = 40;
            typeof(MainWindow).GetMethod("ApplyWindowTransparency", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(main, null);
            Check(Math.Abs(((System.Windows.FrameworkElement)main.FindName("FeaturePanel")).Opacity - 0.6) < 0.001 && ((System.Windows.FrameworkElement)main.FindName("TitleBar")).Opacity == 1 && main.Opacity == 1, "Transparency changes feature pages and footer while header stays opaque");
            opacitySlider.Value = 0;
            typeof(MainWindow).GetMethod("ApplyWindowTransparency", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(main, null);
            typeof(MainWindow).GetField("_loadingSettings", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(main, false);
            var resultGrid = (System.Windows.Controls.DataGrid)main.FindName("AuctionResultsGrid");
            Check(resultGrid.Columns[2].MinWidth >= 56 && resultGrid.Columns[2].Width.IsAuto, "Quantity column sizes to content instead of clipping forty-pixel header");
            resultGrid.ItemsSource = new[] { new AuctionListing { Name = "테스트 검", Count = 1, Price = 200, Options = null }, new AuctionListing { Name = "테스트 활", Count = 2, Price = 100, Options = null } };
            resultGrid.SelectedIndex = 0;
            resultGrid.SelectedIndex = 1;
            var detailsBox = (System.Windows.Controls.TextBox)main.FindName("AuctionDetailsBox");
            var selectionDeadline = DateTime.UtcNow.AddSeconds(4);
            while (DateTime.UtcNow < selectionDeadline && !detailsBox.Text.Contains("현재 등록 물량"))
            {
                main.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);
                System.Threading.Thread.Sleep(10);
            }
            Check(detailsBox.Text.StartsWith("테스트 활") && detailsBox.Text.Contains("현재 등록 물량: 12개") && detailsBox.Text.Contains("최근 판매:") && detailsBox.Text.Contains("상세 옵션 없음"), "Actual grid selections with null options stay responsive and latest selection shows stock/trades");
            content.Measure(new System.Windows.Size(440, 584));
            content.Arrange(new Rect(0, 0, 440, 584));
            content.UpdateLayout();
            var auctionPreview = new System.Windows.Media.Imaging.RenderTargetBitmap(440, 584, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var auctionVisual = new System.Windows.Media.DrawingVisual();
            using (var context = auctionVisual.RenderOpen())
                context.DrawRectangle(new System.Windows.Media.VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            auctionPreview.Render(auctionVisual);
            var auctionEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            auctionEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(auctionPreview));
            using (var file = System.IO.File.Create("auction-preview.png")) auctionEncoder.Save(file);
            var fastPingTab = (System.Windows.Controls.Button)main.FindName("FastPingTabButton");
            fastPingTab.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(((System.Windows.FrameworkElement)main.FindName("FastPingPanel")).Visibility == Visibility.Visible && ((System.Windows.FrameworkElement)main.FindName("BuffPanel")).Visibility == Visibility.Collapsed, "FastPing tab opens");
            content.Measure(new System.Windows.Size(440, 584));
            content.Arrange(new Rect(0, 0, 440, 584));
            content.UpdateLayout();
            var fastPingPreview = new System.Windows.Media.Imaging.RenderTargetBitmap(440, 584, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            var fastPingVisual = new System.Windows.Media.DrawingVisual();
            using (var context = fastPingVisual.RenderOpen())
                context.DrawRectangle(new System.Windows.Media.VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
            fastPingPreview.Render(fastPingVisual);
            var fastPingEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            fastPingEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(fastPingPreview));
            using (var file = System.IO.File.Create("fastping-preview.png")) fastPingEncoder.Save(file);
            var applyInfo = FastPingService.CreateStartInfo(true);
            var revertInfo = FastPingService.CreateStartInfo(false);
            Check(applyInfo.Verb == "runas" && applyInfo.Arguments.Contains("Apply.bat") && revertInfo.Arguments.Contains("Revert.bat"), "FastPing actions point to supplied elevated scripts (not executed)");
            ((System.Windows.Controls.Button)main.FindName("BuffTabButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(((System.Windows.FrameworkElement)main.FindName("BuffPanel")).Visibility == Visibility.Visible, "Buff tab restores");
            ((System.Windows.Controls.Button)main.FindName("UpdateTabButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(((System.Windows.FrameworkElement)main.FindName("UpdatePanel")).Visibility == Visibility.Visible && ((System.Windows.FrameworkElement)main.FindName("ScheduleSiteButton")).Visibility == Visibility.Visible, "Update tab opens with shared schedule footer");
            Check(main.FindName("UpdateFeedBox") == null && main.FindName("OpenUpdateButton") == null && ((System.Windows.Controls.Button)main.FindName("CheckUpdateButton")).Content.ToString() == "최신버전 확인", "Update page keeps one check button without extra configuration controls");
            var currentManifest = System.IO.Path.Combine(AppContext.BaseDirectory, "ui-latest-test.json");
            System.IO.File.WriteAllText(currentManifest, "{\"version\":\"BETA 0.1\"}");
            typeof(MainWindow).GetField("_updateFeedPath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(main, currentManifest);
            var previousContext = System.Threading.SynchronizationContext.Current;
            System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(main.Dispatcher));
            ((System.Windows.Controls.Button)main.FindName("CheckUpdateButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var updateDeadline = DateTime.UtcNow.AddSeconds(3);
            var updateStatus = (System.Windows.Controls.TextBlock)main.FindName("UpdateStatusText");
            while (DateTime.UtcNow < updateDeadline && updateStatus.Text == "최신버전 확인 중…") { main.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle); System.Threading.Thread.Sleep(10); }
            System.Threading.SynchronizationContext.SetSynchronizationContext(previousContext);
            Check(updateStatus.Text == "최신버전입니다.", "Latest-version button reads shared manifest and shows requested exact status");
            System.IO.File.Delete(currentManifest);
            ((System.Windows.Controls.Button)main.FindName("BuffTabButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            var collapse = (System.Windows.Controls.Button)main.FindName("CollapseButton");
            collapse.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(main.Height == 42 && main.WindowState == WindowState.Normal && ((System.Windows.FrameworkElement)main.FindName("FeaturePanel")).Visibility == Visibility.Collapsed, "Collapse keeps title bar on desktop");
            main.Content = null;
            content.Measure(new System.Windows.Size(440, 42));
            content.Arrange(new Rect(0, 0, 440, 42));
            content.UpdateLayout();
            var collapsedPreview = new System.Windows.Media.Imaging.RenderTargetBitmap(440, 42, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            collapsedPreview.Render(content);
            var collapsedEncoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            collapsedEncoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(collapsedPreview));
            using (var file = System.IO.File.Create("collapsed-preview.png")) collapsedEncoder.Save(file);
            main.Content = content;
            collapse.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(main.Height == 584 && main.MinHeight == 564 && ((System.Windows.FrameworkElement)main.FindName("FeaturePanel")).Visibility == Visibility.Visible, "Expand restores feature panel and size");
            main.Close();
            if (System.IO.File.Exists(uiHistoryPath)) System.IO.File.Delete(uiHistoryPath);
            splash.Close();
            app.Shutdown();
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}






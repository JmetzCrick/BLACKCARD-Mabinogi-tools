using System;
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
            if (args.Length > 0 && args[0] == "--updater-fixture") { UpdateInstallTests.Run(Check); return 0; }
            if (args.Length > 0 && args[0] == "--auction-test")
            {
                using var auction = new AuctionService();
                foreach (var keywords in new[] { false, true }) Check(auction.SearchAsync("롱 소드", keywords, null, CancellationToken.None).GetAwaiter().GetResult().Items.Count > 0, "Encrypted shared key retrieves live auction listings");
                return 0;
            }
            CalculatorTests.Run(Check);
            GatheringTests.Run(Check);
            AuctionFeatureTests.Run(Check);
            UpdateFeatureTests.Run(Check);
            if (args.Length == 0) AudioPolicyTests.Run(Check);
            var app = new App();
            app.InitializeComponent();
            var main = new MainWindow(autoPlayMusic: false, auctionDataSource: new FakeAuctionSource(), auctionHistoryPath: Path.Combine(AppContext.BaseDirectory, "ui-history.json"));
            Check(typeof(MainWindow).GetField("_ocr", BindingFlags.NonPublic | BindingFlags.Instance) == null && typeof(MainWindow).Assembly.GetType("BuffAssistant.Services.OcrService") == null, "Public build contains no active OCR service or engine initialization");
            var tab = (Button)main.FindName("BuffTabButton");
            Check(Grid.GetRow(tab) == 1 && Grid.GetColumn(tab) == 3, "Music buff tab moved to bottom-right");
            Check(((TextBlock)main.FindName("BuffWarningIcon")).Text == "❗", "Music buff tab has small warning emoji");
            typeof(MainWindow).GetMethod("BuffTab_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            var menu = (Grid)main.FindName("BuffMenu");
            var notice = (TextBlock)main.FindName("BuffPolicyNotice");
            Check(!menu.IsEnabled && !menu.IsHitTestVisible && menu.Opacity == 0.22, "Original buff menu remains faded and cannot execute actions");
            Check(notice.Text == "넥슨정책상 해당 기능이 지원되지 않습니다." && notice.VerticalAlignment == VerticalAlignment.Center && notice.HorizontalAlignment == HorizontalAlignment.Center && notice.Foreground.ToString() == Brushes.White.ToString(), "Policy notice is centered and white");
            typeof(MainWindow).GetMethod("Start_Click", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(main, new object[] { main, new RoutedEventArgs() });
            Check(((TextBlock)main.FindName("StatusText")).Text == notice.Text, "Direct detection invocation is blocked without OCR or audio");
            var root = (FrameworkElement)main.Content;
            root.Measure(new Size(440,584)); root.Arrange(new Rect(0,0,440,584)); root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(440,584,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
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
            root.Measure(new Size(440,616)); root.Arrange(new Rect(0,0,440,616)); root.UpdateLayout();
            var gatheringBitmap = new RenderTargetBitmap(440,616,96,96,PixelFormats.Pbgra32); gatheringBitmap.Render(root);
            var gatheringEncoder = new PngBitmapEncoder(); gatheringEncoder.Frames.Add(BitmapFrame.Create(gatheringBitmap));
            using (var file = File.Create("gathering-preview.png")) gatheringEncoder.Save(file);
            Check(((TextBlock)main.FindName("HeaderVersionText")).Text == UpdateService.DisplayVersion, "Header version follows release metadata");
            main.Close();
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}

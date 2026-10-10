using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace BuffAssistant;

/// <summary>Independent, non-activating, click-through preview; survives the main window hiding.</summary>
public sealed class GatheringAnnouncementWindow : Window
{
    public static string Format(System.Collections.Generic.IEnumerable<Services.GatheringItem> items) =>
        "에린 시간 30분 후 등장 (현실 약 45초)\n" + string.Join("\n", items.Select(x => $"{x.Name} · {x.StartHour:00}:00 등장"));
    private readonly TextBlock _message;
    private readonly DispatcherTimer _expiry = new() { Interval = TimeSpan.FromSeconds(18) };
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    public GatheringAnnouncementWindow()
    {
        Title = "블랙카드 채집 예고";
        Width = 340;
        SizeToContent = SizeToContent.Height;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        IsHitTestVisible = false;
        FontFamily = (FontFamily)Application.Current.FindResource("GameFont");
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock { Text = "채집 등장 예고 · 만돌린 7채널", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(185,201,255)), Margin = new Thickness(0,0,0,10) });
        _message = new TextBlock { FontSize = 14, Foreground = Brushes.White, TextWrapping = TextWrapping.Wrap, LineHeight = 23 };
        panel.Children.Add(_message);
        Content = new Border { CornerRadius = new CornerRadius(14), Padding = new Thickness(18,14,18,14),
            Background = new LinearGradientBrush(Color.FromArgb(245,22,28,102), Color.FromArgb(245,2,7,21),90), Child = panel };
        SourceInitialized += (_, _) => {
            var hwnd = new WindowInteropHelper(this).Handle;
            SetWindowLong(hwnd,-20,GetWindowLong(hwnd,-20) | 0x20 | 0x80 | 0x08000000); // transparent, toolwindow, noactivate
        };
        _expiry.Tick += (_, _) => Dismiss();
        Closed += (_, _) => _expiry.Stop();
    }
    public void Present(string message)
    {
        _message.Text = message;
        var area = SystemParameters.WorkArea;
        Left = area.Left + 20;
        Top = area.Top + 20;
        Show();
        _expiry.Stop();
        _expiry.Start();
    }
    public void Dismiss() { _expiry.Stop(); Hide(); }
}

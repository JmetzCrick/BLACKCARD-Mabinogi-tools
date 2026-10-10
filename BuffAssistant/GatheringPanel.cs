using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using BuffAssistant.Services;

namespace BuffAssistant;

public sealed class GatheringPanel : UserControl, IDisposable
{
    public void ResetClock() { _tracker.Reset(GameClockSync.Now); Refresh(); }
    private readonly TextBlock _clock = new() { FontSize = 32, Foreground = Brushes.White };
    private readonly TextBlock _summary = new() { FontSize = 10, Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _notice = new() { FontSize = 11, Foreground = Brushes.LightGreen, TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _master = new() { Content = "채집 알람", FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
    private readonly Slider _volume = new() { Minimum = 0, Maximum = 100, Value = 50, Width = 72 };
    private readonly TextBlock _volumeLabel = new() { FontSize = 10, Width = 28, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
    private readonly Dictionary<string, CheckBox> _checks = new();
    private readonly Dictionary<string, TextBlock> _statuses = new();
    private readonly GatheringAlarmTracker _tracker = new();
    private readonly AudioAlertService _sound = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly string _soundPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "Alerts", "Gathering.wav");
    private bool _loading;
    private DateTimeOffset _noticeUntil;
    public event Action? SettingsChanged;
    public bool AlarmsEnabled => _master.IsChecked == true;
    public double VolumePercent => _volume.Value;
    public List<string> SelectedItems => _checks.Where(x => x.Value.IsChecked == true).Select(x => x.Key).ToList();

    public GatheringPanel()
    {
        FontFamily = (FontFamily)Application.Current.FindResource("GameFont");
        Foreground = Brushes.White;
        _master.Style = (Style)Application.Current.FindResource("GatherAlarmToggle");
        var root = new Grid();
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) root.RowDefinitions.Add(new() { Height = height });
        var header = new Grid { Margin = new(0, 0, 0, 6) };
        header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var clockBox = new StackPanel();
        clockBox.Children.Add(new TextBlock { Text = "에린 시간", FontSize = 11, Foreground = Brushes.LightSteelBlue });
        clockBox.Children.Add(_clock); header.Children.Add(clockBox);
        var controls = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        controls.Children.Add(_master);
        var volumeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 8, 0, 0) };
        volumeRow.Children.Add(new TextBlock { Text = "소리", FontSize = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 5, 0) });
        _volume.Style = (Style)Application.Current.FindResource("SimpleSlider");
        volumeRow.Children.Add(_volume); volumeRow.Children.Add(_volumeLabel); controls.Children.Add(volumeRow);
        Grid.SetColumn(controls, 1); header.Children.Add(controls); root.Children.Add(header);
        _summary.Margin = new(0, 0, 0, 9); Grid.SetRow(_summary, 1); root.Children.Add(_summary);
        var legend = new Grid { Margin = new(9, 0, 20, 5) };
        legend.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        legend.ColumnDefinitions.Add(new() { Width = new GridLength(110) });
        legend.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
        var nameHeading = new TextBlock { Text = "채집물 / 에린 시작 → 종료", FontSize = 10, Foreground = Brushes.LightSteelBlue };
        var timerHeading = new TextBlock { Text = "남은 현실 시간", FontSize = 10, Foreground = Brushes.LightSteelBlue, TextAlignment = TextAlignment.Right, Margin = new(0,0,8,0) };
        var alarmHeading = new TextBlock { Text = "알람", FontSize = 10, Foreground = Brushes.LightSteelBlue, TextAlignment = TextAlignment.Center };
        legend.Children.Add(nameHeading); Grid.SetColumn(timerHeading,1); legend.Children.Add(timerHeading); Grid.SetColumn(alarmHeading,2); legend.Children.Add(alarmHeading);
        Grid.SetRow(legend, 2); root.Children.Add(legend);
        var list = new StackPanel();
        foreach (var item in GatheringSchedule.Items)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(110) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(30) });
            var name = new StackPanel();
            name.Children.Add(new TextBlock { Text = item.Name, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            name.Children.Add(new TextBlock { Text = item.ClearTimeRange, FontSize = 10, Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap, Margin = new(0, 3, 0, 0), ToolTip = "에린 시간 " + item.TimeRange });
            row.Children.Add(name);
            var status = new TextBlock { FontSize = 11, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) };
            _statuses[item.Name] = status; Grid.SetColumn(status, 1); row.Children.Add(status);
            var check = new CheckBox { ToolTip = item.Name + " 시작 알람", VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            check.Style = (Style)Application.Current.FindResource("GatherAlarmToggle");
            _checks[item.Name] = check; Grid.SetColumn(check, 2); row.Children.Add(check);
            check.Checked += Changed; check.Unchecked += Changed;
            list.Children.Add(new Border { Background = new SolidColorBrush(Color.FromRgb(11, 16, 40)), CornerRadius = new(10), Padding = new(9, 7, 7, 7), Margin = new(0, 0, 0, 5), ToolTip = item.Skill, Child = row });
        }
        var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Visible, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new(0, 0, 4, 0) };
        scroll.Resources.Add(typeof(ScrollBar), Application.Current.FindResource("CalculatorScrollBar"));
        Grid.SetRow(scroll, 3); root.Children.Add(scroll);
        var foot = new StackPanel { Margin = new(0, 5, 0, 0) };
        foot.Children.Add(_notice);
        foot.Children.Add(new TextBlock { Text = "공식 홈페이지 시계 기준 · 선택한 채집물 시작 시 알림\n프로그램 실행 중 알림 · PC 시간이 정확해야 합니다.", FontSize = 9, Foreground = Brushes.LightSteelBlue, TextWrapping = TextWrapping.Wrap, Margin = new(0, 4, 0, 0) });
        Grid.SetRow(foot, 4); root.Children.Add(foot); Content = root;
        _master.Checked += Changed; _master.Unchecked += Changed;
        _volume.ValueChanged += (_, _) => { _volumeLabel.Text = $"{_volume.Value:0}%"; _sound.SetVolume((float)_volume.Value / 100); if (!_loading) SettingsChanged?.Invoke(); };
        _volumeLabel.Text = "50%";
        _tracker.Reset(GameClockSync.Now);
        _timer.Tick += (_, _) => Refresh(); _timer.Start(); Refresh();
    }

    public void Load(bool enabled, IEnumerable<string> items, double volume)
    {
        _loading = true;
        _master.IsChecked = enabled;
        var selected = items.ToHashSet(StringComparer.Ordinal);
        foreach (var pair in _checks) pair.Value.IsChecked = selected.Contains(pair.Key);
        _volume.Value = Math.Clamp(volume, 0, 100);
        _loading = false;
        _tracker.Reset(GameClockSync.Now); Refresh();
    }
    private void Changed(object sender, RoutedEventArgs args)
    {
        _tracker.Reset(GameClockSync.Now);
        if (!AlarmsEnabled) _sound.Dispose();
        Refresh(); if (!_loading) SettingsChanged?.Invoke();
    }
    public void Refresh()
    {
        var now = GameClockSync.Now;
        _clock.Text = ErinClock.Display(now);
        var next = GatheringSchedule.Items.OrderBy(x => ErinClock.Until(now, x.StartHour)).First();
        _summary.Text = $"지금 {GatheringSchedule.Items.Count(x => x.Available(now))}종 채집 가능 · 다음 {next.Name} {Countdown(ErinClock.Until(now, next.StartHour))} 후";
        foreach (var item in GatheringSchedule.Items)
        {
            var active = item.Available(now);
            var status = _statuses[item.Name];
            status.Text = (active ? "가능 · " : "시작 · ") + Countdown(ErinClock.Until(now, active ? item.EndHour : item.StartHour));
            status.Foreground = active ? Brushes.LightGreen : Brushes.LightSteelBlue;
            status.ToolTip = active ? "채집 종료까지 남은 현실 시간" : "채집 시작까지 남은 현실 시간";
        }
        var alarms = _tracker.Tick(now, AlarmsEnabled, SelectedItems.ToHashSet(StringComparer.Ordinal));
        if (alarms.Count > 0)
        {
            _notice.Text = "채집 시작 · " + string.Join(", ", alarms.Select(x => x.Name));
            _noticeUntil = now.AddSeconds(10);
            try { if (_volume.Value > 0) _sound.Play(_soundPath, (float)_volume.Value / 100); }
            catch { _notice.Text += " · 소리 장치를 확인하세요."; }
            AlarmRaised?.Invoke(_notice.Text);
        }
        else if (now >= _noticeUntil) _notice.Text = AlarmsEnabled ? (SelectedItems.Count == 0 ? "알림받을 채집물을 선택하세요." : $"알람 켜짐 · {SelectedItems.Count}종 선택") : "알람 꺼짐";
    }
    public event Action<string>? AlarmRaised;
    private static string Countdown(TimeSpan span) => $"{(int)Math.Ceiling(span.TotalSeconds) / 60:00}:{(int)Math.Ceiling(span.TotalSeconds) % 60:00}";
    public void Dispose() { _timer.Stop(); _sound.Dispose(); }
}


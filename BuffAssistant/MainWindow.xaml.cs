using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using BuffAssistant.Models;
using BuffAssistant.Services;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace BuffAssistant;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<BuffRule> _rules = new();
    private readonly ObservableCollection<MusicFileItem> _musicFiles = new();
    // 공개 배포 비활성화: private readonly ScreenCaptureService _capture = new();
    // 공개 배포 비활성화: private readonly OcrService _ocr = new();
    // 공개 배포 비활성화: private readonly ColorDetectionService _color = new();
    private readonly AudioAlertService _audio = new();
    private readonly BackgroundMusicService _backgroundMusic = new();
    // 공개 배포 비활성화: private readonly BuffMonitorService _monitor;
    private readonly AppSettingsService _settings = new();
    private readonly AuctionService _auction = new();
    private readonly IAuctionDataSource _auctionSearchSource;
    private readonly AuctionInsightsService _auctionInsights;
    private CancellationTokenSource? _insightsCts;
    private bool _checkingFastPing;
    private readonly UpdateService _updates = new();
    private string _updateFeedPath = "";
    private readonly ObservableCollection<AuctionListing> _auctionItems = new();
    private readonly CancellationTokenSource _auctionLifetime = new();
    private string? _auctionCursor;
    private string _auctionQuery = "";
    private bool _auctionKeywords;
    private bool _auctionBusy;
    private readonly ItemNameCatalog _itemNames = new();
    private bool _choosingItemName;
    private bool _catalogStarted;
    // 공개 배포 비활성화: private CancellationTokenSource? _cts;
    // 공개 배포 비활성화: private ScreenRect? _region;
    private static readonly string DefaultAlertAudio = Path.Combine(
        AppContext.BaseDirectory, "Assets", "Alerts", "음악버프30초.mp3");
    private string? _selectedAudio;
    private bool _loadingSettings = true;
    private bool _musicMuted;
    private bool _musicEnabled = true;
    private bool _closed;
    private bool _featuresCollapsed;
    private double _expandedHeight = 584;
    private double _expandedMinHeight = 564;
    private ResizeMode _expandedResizeMode;
    private readonly System.Windows.Threading.DispatcherTimer _musicUiTimer = new() { Interval = TimeSpan.FromMilliseconds(400) };

    private static readonly string[] DefaultBuffs =
    {
        "전장의 서곡", "비바체", "행진곡", "풍년가"
    };

    public MainWindow(bool autoPlayMusic = true, IAuctionDataSource? auctionDataSource = null, string? auctionHistoryPath = null)
    {
        _auctionSearchSource = auctionDataSource ?? _auction;
        _auctionInsights = new AuctionInsightsService(_auctionSearchSource, auctionHistoryPath);
        InitializeComponent();
        HeaderVersionText.Text = UpdateService.DisplayVersion;
        CurrentVersionText.Text = "현재 버전  " + UpdateService.DisplayVersion;
        AddHandler(System.Windows.Input.Mouse.PreviewMouseDownEvent, new System.Windows.Input.MouseButtonEventHandler(ShowClickRipple), true);
        _ = RefreshFastPingStatusAsync();
        Activated += (_, _) => { if (FastPingPanel.Visibility == Visibility.Visible) _ = RefreshFastPingStatusAsync(); };
        _audio.PlaybackFinished += AlertPlaybackFinished;
        RulesGrid.ItemsSource = _rules;
        AuctionResultsGrid.ItemsSource = _auctionItems;
        MusicList.ItemsSource = _musicFiles;
        BuffNameBox.ItemsSource = DefaultBuffs;
        BuffNameBox.SelectedIndex = 0;
        AudioPathBox.Text = DefaultAlertAudio;
        // 공개 배포 비활성화: _monitor = new BuffMonitorService(_capture, _ocr, _color, OnMonitorEvent);

        foreach (var name in DefaultBuffs)
        {
            _rules.Add(new BuffRule
            {
                Name = name,
                AudioFile = DefaultAlertAudio,
                RedPixelRatioThreshold = 0.12,
                RequiredConsecutiveDetections = 2
            });
        }

        LoadSettings(autoPlayMusic);
        SelectFeatureTab(true);
        if (autoPlayMusic) Loaded += async (_, _) => await CheckStartupUpdateAsync();
        UpdateMusicButtons();
        _musicUiTimer.Tick += (_, _) => UpdateMusicButtons();
        _musicUiTimer.Start();
        RefreshRules();
        Closed += (_, _) =>
        {
            _closed = true;
            ClickEffectsCanvas.Children.Clear();
            _auctionLifetime.Cancel();
            _insightsCts?.Cancel();
            _auction.Dispose();
            _updates.Dispose();
            _audio.PlaybackFinished -= AlertPlaybackFinished;
            // 공개 배포 비활성화: _cts?.Cancel();
            _musicUiTimer.Stop();
            _backgroundMusic.Dispose();
            _audio.Dispose();
            // 공개 배포 비활성화: _ocr.Dispose();
        };
    }

    private void LoadSettings(bool autoPlayMusic)
    {
        _loadingSettings = true;
        try
        {
            var settings = _settings.Load();
            var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "Music", "Etain.mp3");
            foreach (var path in MusicCatalog.Normalize(settings.MusicFiles, bundled))
                _musicFiles.Add(new MusicFileItem(path));

            foreach (var saved in settings.BuffRules)
            {
                var existing = _rules.FirstOrDefault(r => r.Name.Equals(saved.Name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.AudioFile = string.IsNullOrWhiteSpace(saved.AudioFile) ? DefaultAlertAudio : saved.AudioFile;
                    existing.RedPixelRatioThreshold = saved.RedPixelRatioThreshold;
                    existing.RequiredConsecutiveDetections = saved.RequiredConsecutiveDetections;
                }
            }

            MusicList.SelectedItem = _musicFiles.FirstOrDefault(m => m.Path.Equals(settings.SelectedMusicFile, StringComparison.OrdinalIgnoreCase)) ?? _musicFiles.FirstOrDefault();
            _backgroundMusic.Repeat = settings.MusicRepeat;
            _musicMuted = settings.MusicMuted;
            _musicEnabled = settings.ShouldPlayMusic;
            _backgroundMusic.SetMuted(_musicMuted);
            MusicVolumeSlider.Value = settings.StartupMusicVolumePercent;
            WindowTransparencySlider.Value = Math.Clamp(settings.WindowTransparencyPercent, 0, 80);
            ApplyWindowTransparency();
            _updateFeedPath = string.IsNullOrEmpty(settings.UpdateFeedUrl) ? UpdateService.SuggestedFeedPath : settings.UpdateFeedUrl;
            try { _updateFeedPath = UpdateService.ConfiguredFeed(_updateFeedPath); } catch { UpdateStatusText.Text = "업데이트 배포 설정 파일을 확인하세요."; }
            try { _updateFeedPath = UpdateService.GitHubFeed ?? _updateFeedPath; } catch { UpdateStatusText.Text = "GitHub 업데이트 설정을 확인하세요."; }
            AlertVolumeSlider.Value = Math.Clamp(settings.AlertVolumePercent, 0, 100);
            AlertVolumeValueText.Text = $"{AlertVolumeSlider.Value:F0}%";
            UpdateVolumeLabel();
        }
        finally { _loadingSettings = false; }

        if (autoPlayMusic && _musicEnabled && _musicFiles.Count > 0)
            PlayCurrentMusic();
    }

    private void SaveSettings()
    {
        _settings.Save(new AppSettings
        {
            MusicFiles = _musicFiles.Select(x => x.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            MusicEnabled = _musicEnabled,
            MusicPlaybackStateSaved = true,
            MusicRepeat = _backgroundMusic.Repeat,
            MusicMuted = _musicMuted,
            SelectedMusicFile = (MusicList.SelectedItem as MusicFileItem)?.Path,
            MusicVolumePercent = MusicVolumeSlider.Value,
            MusicVolumeDefaultsVersion = 1,
            AlertVolumePercent = AlertVolumeSlider.Value,
            WindowTransparencyPercent = WindowTransparencySlider.Value,
            UpdateFeedUrl = _updateFeedPath,
            BuffRules = _rules.Select(r => new SavedBuffRule
            {
                Name = r.Name,
                AudioFile = r.AudioFile,
                RedPixelRatioThreshold = r.RedPixelRatioThreshold,
                RequiredConsecutiveDetections = r.RequiredConsecutiveDetections
            }).ToList()
        });
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
//     private async void SelectRegion_Click(object sender, RoutedEventArgs e)
//     {
//         Hide();
//         await Task.Delay(250);
//         var picker = new RegionPickerWindow();
//         if (picker.ShowDialog() == true && picker.SelectedRect is not null)
//         {
//             _region = picker.SelectedRect;
//             RegionText.Text = $"감지 영역: X={_region.X}, Y={_region.Y}, W={_region.Width}, H={_region.Height}";
//             AddLog("감지 영역이 설정되었습니다.");
//         }
//         Show();
//         Activate();
//     }
// 
// 
    private void ChooseAudio_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "버프 알림 음성 파일 선택",
            Filter = "오디오 파일 (*.wav;*.mp3)|*.wav;*.mp3|WAV 파일 (*.wav)|*.wav|MP3 파일 (*.mp3)|*.mp3",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            _selectedAudio = dialog.FileName;
            AudioPathBox.Text = _selectedAudio;
        }
    }

    private void AddRule_Click(object sender, RoutedEventArgs e)
    {
        var name = (BuffNameBox.Text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("버프명을 입력하거나 목록에서 선택하세요.");
            return;
        }

        var existing = _rules.FirstOrDefault(r => r.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _rules.Add(new BuffRule
            {
                Name = name,
                AudioFile = _selectedAudio ?? DefaultAlertAudio,
                RedPixelRatioThreshold = 0.12,
                RequiredConsecutiveDetections = 2
            });
        }
        else
        {
            existing.AudioFile = _selectedAudio ?? existing.AudioFile;
            existing.ResetAlert();
        }
        RefreshRules();
        SaveSettings();
        AddLog($"규칙 저장: {name}");
    }

    private void RefreshRules() => RulesGrid.Items.Refresh();

    private void Start_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
//     private async void Start_Click(object sender, RoutedEventArgs e)
//     {
//         if (_region is null)
//         {
//             MessageBox.Show("먼저 감지 영역을 선택하세요.");
//             return;
//         }
//         if (!_rules.Any())
//         {
//             MessageBox.Show("버프 규칙을 하나 이상 추가하세요.");
//             return;
//         }
// 
//         _cts = new CancellationTokenSource();
//         StartButton.IsEnabled = false;
//         StopButton.IsEnabled = true;
//         StatusText.Text = "감지 중";
//         AddLog("감지를 시작했습니다.");
// 
//         try { await _monitor.RunAsync(_region, _rules.ToList(), _cts.Token); }
//         catch (OperationCanceledException) { }
//         catch (Exception ex)
//         {
//             AddLog("오류: " + ex.Message);
//             MessageBox.Show(ex.Message, "감지 오류");
//         }
//         finally
//         {
//             StartButton.IsEnabled = true;
//             StopButton.IsEnabled = false;
//             StatusText.Text = "중지됨";
//         }
//     }
// 
// 
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        // 공개 배포 비활성화: _cts?.Cancel();
        AddLog("감지 중지를 요청했습니다.");
    }

    private void MusicTab_Click(object sender, RoutedEventArgs e) => MusicPopup.IsOpen = !MusicPopup.IsOpen;
    private void BuffTab_Click(object sender, RoutedEventArgs e) => SelectFeatureTab(false);
    private async void FastPingTab_Click(object sender, RoutedEventArgs e)
    {
        SelectFeatureTab(true);
        await RefreshFastPingStatusAsync();
    }
    private async Task RefreshFastPingStatusAsync()
    {
        if (_checkingFastPing || _closed) return;
        _checkingFastPing = true;
        try
        {
            var applied = await Task.Run(FastPingService.IsApplied);
            await Dispatcher.InvokeAsync(() =>
            {
                if (_closed) return;
                FastPingAppliedText.Text = applied ? "이미 적용되어 있습니다." : "패스트핑이 적용되어있지 않습니다";
                FastPingAppliedText.Foreground = new System.Windows.Media.SolidColorBrush(applied
                    ? System.Windows.Media.Color.FromRgb(83, 214, 155) : System.Windows.Media.Color.FromRgb(255, 107, 118));
            });
        }
        catch (Exception)
        {
            await Dispatcher.InvokeAsync(() => { if (!_closed) { FastPingAppliedText.Text = "패스트핑 상태를 확인할 수 없습니다."; FastPingAppliedText.Foreground = System.Windows.Media.Brushes.Orange; } });
        }
        finally { _checkingFastPing = false; }
    }
    private void SelectFeatureTab(bool fastPing)
    {
        BeadPanel.Visibility = Visibility.Collapsed;
        FeePanel.Visibility = Visibility.Collapsed;
        BeadTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        FeeTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        UpdatePanel.Visibility = Visibility.Collapsed;
        AuctionPanel.Visibility = Visibility.Collapsed;
        AuctionSuggestionsPopup.IsOpen = false;
        BuffPanel.Visibility = fastPing ? Visibility.Collapsed : Visibility.Visible;
        FastPingPanel.Visibility = fastPing ? Visibility.Visible : Visibility.Collapsed;
        var active = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(49, 75, 166));
        var inactive = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        BuffTabButton.Background = fastPing ? inactive : active;
        FastPingTabButton.Background = fastPing ? active : inactive;
        AuctionTabButton.Background = inactive;
        UpdateTabButton.Background = inactive;
    }
    private void UpdateTab_Click(object sender, RoutedEventArgs e)
    {
        SelectFeatureTab(false);
        BuffPanel.Visibility = Visibility.Collapsed;
        UpdatePanel.Visibility = Visibility.Visible;
        UpdateTabButton.Background = BuffTabButton.Background;
        BuffTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
    }
    private void BeadTab_Click(object sender, RoutedEventArgs e) => ShowCalculator(true);
    private void FeeTab_Click(object sender, RoutedEventArgs e) => ShowCalculator(false);
    private void ShowCalculator(bool beads)
    {
        SelectFeatureTab(false);
        BuffPanel.Visibility = Visibility.Collapsed;
        var panel = beads ? BeadPanel : FeePanel;
        panel.Child ??= new CalculatorPanel(beads);
        panel.Visibility = Visibility.Visible;
        (beads ? BeadTabButton : FeeTabButton).Background = BuffTabButton.Background;
        BuffTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
    }
    private void WindowTransparency_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingSettings) return;
        ApplyWindowTransparency();
        SaveSettings();
    }
    private async void CheckUpdate_Click(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Foreground = (System.Windows.Media.Brush)FindResource("Muted");
        UpdateStatusText.Text = "최신버전 확인 중…";
        try
        {
            var result = await _updates.CheckAsync(_updateFeedPath, _auctionLifetime.Token);
            if (_closed) return;
            if (!result.Available)
            {
                UpdateStatusText.Text = "최신버전입니다.";
                UpdateStatusText.Foreground = System.Windows.Media.Brushes.LightGreen;
                CurrentVersionText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 201, 255));
                return;
            }
            UpdateStatusText.Text = "최신버전이 아닙니다 업데이트 해주세요!";
            UpdateStatusText.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 107, 118));
            CurrentVersionText.Foreground = UpdateStatusText.Foreground;
            if (MessageBox.Show(this, "업데이트가 있습니다. 프로그램을 업데이트 하시겠습니까?", "블랙카드 도우미", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK) return;
            UpdateStatusText.Text = "업데이트 파일을 준비 중입니다…";
            var plan = await UpdateInstaller.PrepareAsync(result, AppContext.BaseDirectory, null, _auctionLifetime.Token);
            if (_closed) return;
            SaveSettings();
            UpdateInstaller.Start(plan);
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException) { if (!_closed) UpdateStatusText.Text = "업데이트가 취소되었습니다."; }
        catch (Exception ex)
        {
            if (!_closed) { UpdateStatusText.Text = "업데이트를 확인하거나 적용하지 못했습니다.\n" + (ex is IOException || ex is UnauthorizedAccessException ? "설치 폴더의 쓰기 권한을 확인하세요." : ex.Message); UpdateStatusText.Foreground = System.Windows.Media.Brushes.Salmon; }
        }
        finally { if (!_closed) CheckUpdateButton.IsEnabled = true; }
    }
    private async Task CheckStartupUpdateAsync()
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "GitHub 최신버전 확인 중…";
        try
        {
            var result = await _updates.CheckAsync(_updateFeedPath, _auctionLifetime.Token);
            if (_closed) return;
            UpdateStatusText.Text = result.Available ? "최신버전이 아닙니다 업데이트 해주세요!" : "최신버전입니다.";
            UpdateStatusText.Foreground = result.Available ? System.Windows.Media.Brushes.Salmon : System.Windows.Media.Brushes.LightGreen;
            CurrentVersionText.Foreground = UpdateStatusText.Foreground;
            if (result.Available) { UpdateTabButton.Content = "업데이트 ●"; UpdateTabButton.ToolTip = "새 버전 " + result.Info.Version; }
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (!_closed) { UpdateStatusText.Text = "자동 확인에 실패했습니다. 최신버전 확인 버튼으로 다시 시도하세요."; UpdateStatusText.Foreground = System.Windows.Media.Brushes.Salmon; } }
        finally { if (!_closed) CheckUpdateButton.IsEnabled = true; }
    }
    private void ApplyWindowTransparency()
    {
        var opacity = 1 - WindowTransparencySlider.Value / 100;
        FeaturePanel.Opacity = opacity;
        ClickEffectsCanvas.Opacity = opacity;
        WindowTransparencyText.Text = $"{WindowTransparencySlider.Value:F0}%";
    }
    private void AuctionTab_Click(object sender, RoutedEventArgs e)
    {
        SelectFeatureTab(false);
        BuffPanel.Visibility = Visibility.Collapsed;
        AuctionPanel.Visibility = Visibility.Visible;
        AuctionTabButton.Background = BuffTabButton.Background;
        BuffTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        if (!_catalogStarted) { _catalogStarted = true; _ = RefreshItemNamesAsync(); }
    }
    private void AuctionKey_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Window { Owner = this, Title = "넥슨 Open API 키", Width = 370, Height = 225, ResizeMode = ResizeMode.NoResize, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = (System.Windows.Media.Brush)FindResource("NavyGradient") };
        var body = new StackPanel { Margin = new Thickness(18) };
        body.Children.Add(new TextBlock { Text = "마비노기 접근 권한이 있는 API 키를 입력하세요.\n키는 현재 Windows 계정에 암호화하여 저장합니다.", FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
        var input = new PasswordBox { Padding = new Thickness(8), Background = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(input);
        var status = new TextBlock { FontSize = 10, Foreground = System.Windows.Media.Brushes.Salmon, TextWrapping = TextWrapping.Wrap };
        var save = new Button { Content = "저장", Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 5) };
        save.Click += (_, _) => { try { AuctionService.SaveKey(input.Password); input.Clear(); dialog.DialogResult = true; } catch (Exception) { status.Text = "키 형식 또는 저장 권한을 확인하세요."; } };
        body.Children.Add(save);
        body.Children.Add(status);
        dialog.Content = body;
        if (dialog.ShowDialog() == true) { AuctionStatusText.Text = "API 키를 저장했습니다. 검색할 수 있습니다."; _catalogStarted = true; _ = RefreshItemNamesAsync(); }
    }
    private async Task RefreshItemNamesAsync()
    {
        try { await _itemNames.RefreshAsync(_auction, () => { if (!_closed && AuctionQueryBox.IsKeyboardFocusWithin) UpdateItemSuggestions(); }, _auctionLifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception) { /* Offline suggestions remain available; manual searches show connection errors. */ }
    }
    private void AuctionQuery_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_choosingItemName && AuctionSuggestionsPopup is not null) UpdateItemSuggestions();
    }
    private void UpdateItemSuggestions()
    {
        if (_closed || _choosingItemName || _auctionBusy || AuctionSuggestionsList is null) return;
        var matches = _itemNames.Suggest(AuctionQueryBox.Text);
        AuctionSuggestionsList.ItemsSource = matches;
        AuctionSuggestionHint.Text = matches.Count == 50 ? "추천 50개 · 더 입력하면 목록이 좁혀집니다." : "이름을 선택하면 매물을 검색합니다.";
        AuctionSuggestionsPopup.IsOpen = matches.Count > 0 && AuctionPanel.Visibility == Visibility.Visible && AuctionQueryBox.IsKeyboardFocusWithin;
    }
    private async Task ChooseSuggestedItemAsync()
    {
        if (AuctionSuggestionsList.SelectedItem is not string name) return;
        _choosingItemName = true;
        try
        {
            AuctionSuggestionsPopup.IsOpen = false;
            AuctionQueryBox.Text = name;
            AuctionQueryBox.CaretIndex = name.Length;
            AuctionModeBox.SelectedIndex = 0;
        }
        finally { _choosingItemName = false; }
        await SearchAuctionAsync(false);
    }
    private async void AuctionSuggestion_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject original && System.Windows.Controls.ItemsControl.ContainerFromElement(AuctionSuggestionsList, original) is System.Windows.Controls.ListBoxItem)
        { e.Handled = true; await ChooseSuggestedItemAsync(); }
    }
    private async void AuctionSuggestions_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await ChooseSuggestedItemAsync(); }
        else if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; AuctionSuggestionsPopup.IsOpen = false; AuctionQueryBox.Focus(); }
    }
    private async void AuctionSearch_Click(object sender, RoutedEventArgs e) => await SearchAuctionAsync(false);
    private async void AuctionMore_Click(object sender, RoutedEventArgs e) => await SearchAuctionAsync(true);
    private async void AuctionQuery_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Down && AuctionSuggestionsPopup.IsOpen)
        {
            e.Handled = true;
            AuctionSuggestionsList.SelectedIndex = 0;
            AuctionSuggestionsList.UpdateLayout();
            (AuctionSuggestionsList.ItemContainerGenerator.ContainerFromIndex(0) as ListBoxItem)?.Focus();
            return;
        }
        if (e.Key == System.Windows.Input.Key.Escape) { e.Handled = true; AuctionSuggestionsPopup.IsOpen = false; return; }
        if (e.Key == System.Windows.Input.Key.Enter) { e.Handled = true; await SearchAuctionAsync(false); }
    }
    private async void AuctionResults_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (AuctionDetailsBox is null) return;
        _insightsCts?.Cancel();
        _insightsCts?.Dispose();
        _insightsCts = null;
        var item = AuctionResultsGrid.SelectedItem as AuctionListing;
        if (item is null) { AuctionDetailsBox.Text = "매물을 선택하면 물량·최근 거래·상세 옵션이 표시됩니다."; return; }
        var request = CancellationTokenSource.CreateLinkedTokenSource(_auctionLifetime.Token);
        request.CancelAfter(TimeSpan.FromSeconds(45));
        _insightsCts = request;
        try
        {
            AuctionDetailsBox.Text = $"{item.Title}\n현재 물량과 최근 거래를 조회 중…\n선택 매물: 개당 {item.Price:N0} 골드 · {item.Count:N0}개\n{item.Details}";
            var insights = await _auctionInsights.LoadAsync(item.Name, request.Token);
            DisplayAuctionDetails(request, insights.Report + $"\n\n선택 매물: 개당 {item.Price:N0} 골드 · {item.Count:N0}개 · 만료 {item.ExpiryText}\n{item.Details}");
        }
        catch (OperationCanceledException)
        {
            DisplayAuctionDetails(request, $"조회 시간이 초과되었습니다. 다른 매물을 선택하거나 다시 검색하세요.\n{item.Details}");
        }
        catch (Exception ex)
        {
            DisplayAuctionDetails(request, "매물 정보를 조회할 수 없습니다.\n" + ex.Message);
        }
    }
    private void DisplayAuctionDetails(CancellationTokenSource request, string text)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(() => DisplayAuctionDetails(request, text))); return; }
        if (!_closed && ReferenceEquals(request, _insightsCts)) AuctionDetailsBox.Text = text;
    }
    private async Task SearchAuctionAsync(bool more)
    {
        if (_auctionBusy || (more && string.IsNullOrEmpty(_auctionCursor))) return;
        _auctionBusy = true;
        AuctionSuggestionsPopup.IsOpen = false;
        AuctionSearchButton.IsEnabled = AuctionMoreButton.IsEnabled = false;
        try
        {
            if (!more)
            {
                _auctionQuery = AuctionQueryBox.Text.Trim();
                _auctionKeywords = AuctionModeBox.SelectedIndex == 1;
                _auctionCursor = null;
                _auctionItems.Clear();
            }
            AuctionStatusText.Text = "검색 중…";
            var page = await _auctionSearchSource.SearchAsync(_auctionQuery, _auctionKeywords, more ? _auctionCursor : null, _auctionLifetime.Token);
            if (_closed) return;
            foreach (var item in page.Items) _auctionItems.Add(item);
            _itemNames.Add(page.Items.Select(item => item.Name));
            _auctionCursor = page.NextCursor;
            AuctionStatusText.Text = $"{_auctionItems.Count:N0}개 · {DateTime.Now:HH:mm} 조회";
        }
        catch (OperationCanceledException) { if (!_closed) AuctionStatusText.Text = "연결 시간이 초과되었습니다."; }
        catch (Exception ex) { if (!_closed) { AuctionStatusText.Text = "검색 실패"; AuctionDetailsBox.Text = ex is System.Net.Http.HttpRequestException ? "인터넷 연결을 확인하세요." : ex.Message; } }
        finally
        {
            _auctionBusy = false;
            if (!_closed) { AuctionSearchButton.IsEnabled = true; AuctionMoreButton.IsEnabled = !string.IsNullOrEmpty(_auctionCursor); }
        }
    }
    private async void ApplyFastPing_Click(object sender, RoutedEventArgs e) => await RunFastPingAsync(true);
    private async void RevertFastPing_Click(object sender, RoutedEventArgs e) => await RunFastPingAsync(false);
    private async Task RunFastPingAsync(bool apply)
    {
        ApplyFastPingButton.IsEnabled = RevertFastPingButton.IsEnabled = false;
        try
        {
            FastPingStatusText.Text = "관리자 권한 요청 중…";
            using var process = System.Diagnostics.Process.Start(FastPingService.CreateStartInfo(apply));
            if (process is null) throw new InvalidOperationException("배치파일을 실행할 수 없습니다.");
            FastPingStatusText.Text = "배치 실행 중 · 명령 창의 결과를 확인하세요.";
            await process.WaitForExitAsync();
            FastPingStatusText.Text = process.ExitCode == 0
                ? "배치 실행 종료 · 명령 창 결과 확인 후 재부팅하세요."
                : $"배치 실행 종료 (코드 {process.ExitCode}) · 명령 창의 오류를 확인하세요.";
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        { FastPingStatusText.Text = "관리자 권한 요청이 취소되었습니다."; }
        catch (Exception ex) { FastPingStatusText.Text = "실행 오류: " + ex.Message; }
        finally
        {
            if (!_closed) { ApplyFastPingButton.IsEnabled = RevertFastPingButton.IsEnabled = true; await RefreshFastPingStatusAsync(); }
        }
    }
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        MusicPopup.IsOpen = false;
        AuctionSuggestionsPopup.IsOpen = false;
        if (!_featuresCollapsed)
        {
            _expandedHeight = ActualHeight > 0 ? ActualHeight : Height;
            _expandedMinHeight = MinHeight;
            _expandedResizeMode = ResizeMode;
            FeaturePanel.Visibility = Visibility.Collapsed;
            MinHeight = 42;
            Height = 42;
            ResizeMode = ResizeMode.NoResize;
            CollapseButton.Content = "\uE70D";
            CollapseButton.ToolTip = "기능창 펼치기";
        }
        else
        {
            MinHeight = _expandedMinHeight;
            Height = _expandedHeight;
            ResizeMode = _expandedResizeMode;
            FeaturePanel.Visibility = Visibility.Visible;
            CollapseButton.Content = "\uE921";
            CollapseButton.ToolTip = "기능창 접기";
        }
        _featuresCollapsed = !_featuresCollapsed;
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void ShowClickRipple(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_closed || e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        var point = e.GetPosition(ClickEffectsCanvas);
        if (point.Y < 42 || point.X < 0 || point.Y > ClickEffectsCanvas.ActualHeight || point.X > ClickEffectsCanvas.ActualWidth) return;
        while (ClickEffectsCanvas.Children.Count >= 12) ClickEffectsCanvas.Children.RemoveAt(0);
        var ring = new System.Windows.Shapes.Ellipse
        {
            Width = 64, Height = 64, StrokeThickness = 1.1,
            Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(185, 211, 255)),
            Fill = new System.Windows.Media.RadialGradientBrush(System.Windows.Media.Color.FromArgb(18, 163, 198, 255), System.Windows.Media.Colors.Transparent),
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new System.Windows.Media.ScaleTransform(0.7, 0.7), IsHitTestVisible = false
        };
        Canvas.SetLeft(ring, point.X - 32);
        Canvas.SetTop(ring, point.Y - 32);
        ClickEffectsCanvas.Children.Add(ring);
        var scale = new System.Windows.Media.Animation.DoubleAnimationUsingKeyFrames { Duration = TimeSpan.FromMilliseconds(650) };
        scale.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.7, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.Zero)));
        scale.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(0.15, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(130)), new System.Windows.Media.Animation.QuadraticEase()));
        scale.KeyFrames.Add(new System.Windows.Media.Animation.EasingDoubleKeyFrame(1.6, System.Windows.Media.Animation.KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(650)), new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }));
        var transform = (System.Windows.Media.ScaleTransform)ring.RenderTransform;
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, scale);
        transform.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, scale);
        var fade = new System.Windows.Media.Animation.DoubleAnimation(0.65, 0, TimeSpan.FromMilliseconds(650));
        fade.Completed += (_, _) => ClickEffectsCanvas.Children.Remove(ring);
        ring.BeginAnimation(OpacityProperty, fade);
    }
    private void ScheduleSite_Click(object sender, RoutedEventArgs e) => OpenExternalTarget("https://black-card-hunt.jakekik.chatgpt.site/");
    private void OpenExternalTarget(string target)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(target)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"사이트를 열 수 없습니다.\n{ex.Message}", "블랙카드 도우미");
        }
    }
    private void Repeat_Click(object sender, RoutedEventArgs e)
    {
        _backgroundMusic.Repeat = !_backgroundMusic.Repeat;
        UpdateMusicButtons();
        SaveSettings();
    }
    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        _musicMuted = !_musicMuted;
        _backgroundMusic.SetMuted(_musicMuted);
        UpdateMusicButtons();
        SaveSettings();
    }
    private void PlayPause_Click(object sender, RoutedEventArgs e)
    {
        if (_backgroundMusic.IsPlaybackRequested)
        {
            _musicEnabled = false;
            _backgroundMusic.Pause();
        }
        else
        {
            _musicEnabled = true;
            if (_backgroundMusic.CurrentFile is not null) _backgroundMusic.Resume();
            else PlayCurrentMusic();
        }
        UpdateMusicButtons();
        SaveSettings();
    }
    private void UpdateMusicButtons()
    {
        PlayPauseButton.Content = _backgroundMusic.IsPlaybackRequested ? "\uE769" : "\uE768";
        PlayPauseButton.ToolTip = _backgroundMusic.IsPlaybackRequested ? "일시정지" : "재생";
        RepeatButton.Foreground = new System.Windows.Media.SolidColorBrush(_backgroundMusic.Repeat ? System.Windows.Media.Color.FromRgb(185, 201, 255) : System.Windows.Media.Color.FromRgb(98, 108, 130));
        RepeatButton.ToolTip = _backgroundMusic.Repeat ? "반복 켜짐" : "반복 꺼짐";
        MuteButton.Content = _musicMuted ? "\uE74F" : "\uE995";
        MuteButton.ToolTip = _musicMuted ? "음소거 해제" : "음소거";
    }
    private void AddMusic_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "음악 추가", Filter = "음악 파일|*.mp3;*.wav;*.aiff;*.wma", Multiselect = true };
        if (dialog.ShowDialog() != true) return;
        var selected = (MusicList.SelectedItem as MusicFileItem)?.Path;
        _loadingSettings = true;
        try
        {
            var paths = MusicCatalog.Normalize(_musicFiles.Select(m => m.Path).Concat(dialog.FileNames));
            _musicFiles.Clear();
            foreach (var path in paths) _musicFiles.Add(new MusicFileItem(path));
            MusicList.SelectedItem = _musicFiles.FirstOrDefault(m => m.Path == selected) ?? _musicFiles.FirstOrDefault();
        }
        finally { _loadingSettings = false; }
        SaveSettings();
    }
    private void MusicList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loadingSettings) return;
        if (_musicEnabled) PlayCurrentMusic();
        SaveSettings();
    }
    private void MusicVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingSettings) return;
        UpdateVolumeLabel();
        _backgroundMusic.SetVolumePercent(MusicVolumeSlider.Value);
        SaveSettings();
    }
    private void UpdateVolumeLabel() => VolumeValueText.Text = $"{MusicVolumeSlider.Value:F0}%";
    private void AlertVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingSettings) return;
        AlertVolumeValueText.Text = $"{AlertVolumeSlider.Value:F0}%";
        _audio.SetVolume((float)(AlertVolumeSlider.Value / 100));
        SaveSettings();
    }
    private void PlayCurrentMusic()
    {
        var file = (MusicList.SelectedItem as MusicFileItem)?.Path ?? _musicFiles.FirstOrDefault()?.Path;
        if (file is null) return;
        try
        {
            _backgroundMusic.PlayPlaylist(_musicFiles.Select(m => m.Path).ToList(), file, MusicVolumeSlider.Value);
            MusicTabButton.ToolTip = Path.GetFileName(file);
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "음악 재생 오류"); }
        UpdateMusicButtons();
    }
//     private void OnMonitorEvent(MonitorEvent ev)
//     {
//         Dispatcher.Invoke(() =>
//         {
//             AddLog(ev.Message);
//             StatusText.Text = ev.AudioFile is null ? ev.Message : "30초 알림";
//             if (ev.AudioFile is not null && File.Exists(ev.AudioFile))
//             {
//                 try
//                 {
//                     _backgroundMusic.SetAlertActive(true);
//                     _audio.Play(ev.AudioFile, (float)(AlertVolumeSlider.Value / 100));
//                     if (!_audio.IsPlaying) _backgroundMusic.SetAlertActive(false);
//                 }
//                 catch (Exception ex)
//                 {
//                     _backgroundMusic.SetAlertActive(false);
//                     AddLog("알림 음성 오류: " + ex.Message);
//                 }
//             }
//         });
//     }
// 
// 
    private void AlertPlaybackFinished(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (_closed || _audio.IsPlaying) return;
            _backgroundMusic.SetAlertActive(false);
            UpdateMusicButtons();
        }));
    }

    private void AddLog(string message)
    {
        StatusText.ToolTip = message;
    }
}

public sealed class MusicFileItem
{
    public string Path { get; }
    public string DisplayName => System.IO.Path.GetFileName(Path).Equals("Etain.mp3", StringComparison.OrdinalIgnoreCase)
        ? "여름 들장미의 향기 · 에탄 BGM" : System.IO.Path.GetFileName(Path);
    public MusicFileItem(string path) => Path = path;
}









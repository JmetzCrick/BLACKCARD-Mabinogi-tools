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
    private readonly BackgroundMusicService _backgroundMusic = new();
    private readonly AppSettingsService _settings = new();
    private GatheringPanel _gathering = null!;
    private GatheringAnnouncementWindow? _gatheringAnnouncement;
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
    private static readonly string DefaultAlertAudio = Path.Combine(
        AppContext.BaseDirectory, "Assets", "Alerts", "음악버프30초.mp3");
    private bool _loadingSettings = true;
    private bool _musicMuted;
    private bool _musicEnabled = true;
    private bool _closed;
    private bool _featuresCollapsed;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _allowExit;
    private double _expandedHeight = 680;
    private double _expandedMinHeight = 660;
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
        _gathering = new GatheringPanel();
        GatheringHost.Child = _gathering;
        _gathering.SettingsChanged += () => { if (!_gathering.AlarmsEnabled) _gatheringAnnouncement?.Dismiss(); if (!_loadingSettings) SaveSettings(); };
        _gathering.AlarmRaised += message => { (_gatheringAnnouncement ??= new GatheringAnnouncementWindow()).Present(message); };
        HeaderVersionText.Text = UpdateService.DisplayVersion;
        CurrentVersionText.Text = "현재 버전  " + UpdateService.DisplayVersion;
        AddHandler(System.Windows.Input.Mouse.PreviewMouseDownEvent, new System.Windows.Input.MouseButtonEventHandler(ShowClickRipple), true);
        _ = RefreshFastPingStatusAsync();
        Activated += (_, _) => { if (FastPingPanel.Visibility == Visibility.Visible) _ = RefreshFastPingStatusAsync(); };
        RulesGrid.ItemsSource = _rules;
        AuctionResultsGrid.ItemsSource = _auctionItems;
        MusicList.ItemsSource = _musicFiles;
        BuffNameBox.ItemsSource = DefaultBuffs;
        BuffNameBox.SelectedIndex = 0;
        AudioPathBox.Text = DefaultAlertAudio;

        foreach (var name in DefaultBuffs)
        {
            _rules.Add(new BuffRule
            {
                Name = name,
                AudioFile = DefaultAlertAudio,
            });
        }

        LoadSettings(autoPlayMusic);
        if (autoPlayMusic) Loaded += async (_, _) => await SyncClockAsync();
        if (autoPlayMusic)
        {
            InitializeTray();
            Loaded += (_, _) => { if (StartBackgroundCheck.IsChecked == true || Environment.GetCommandLineArgs().Contains("--background")) HideToTray(); };
            Closing += (_, args) => { if (!_allowExit && CloseToTrayCheck.IsChecked == true) { args.Cancel = true; HideToTray(); } };
        }
        SelectFeatureTab(true);
        if (autoPlayMusic) Loaded += async (_, _) => await CheckStartupUpdateAsync();
        UpdateMusicButtons();
        _musicUiTimer.Tick += (_, _) => { UpdateMusicButtons(); UpdateErinClock(); };
        UpdateErinClock();
        _musicUiTimer.Start();
        RefreshRules();
        Closed += (_, _) =>
        {
            _closed = true;
            _tray?.Dispose();
            _gathering.Dispose();
            _gatheringAnnouncement?.Close();
            ClickEffectsCanvas.Children.Clear();
            _auctionLifetime.Cancel();
            _insightsCts?.Cancel();
            _auction.Dispose();
            _updates.Dispose();
            _musicUiTimer.Stop();
            _backgroundMusic.Dispose();
        };
    }

    private void LoadSettings(bool autoPlayMusic)
    {
        _loadingSettings = true;
        try
        {
            var settings = _settings.Load();
            ErinClock.CorrectionMilliseconds = Math.Clamp(settings.ErinClockCorrectionMilliseconds, -ErinClock.DayMilliseconds, ErinClock.DayMilliseconds);
            StartBackgroundCheck.IsChecked = settings.StartInBackground;
            CloseToTrayCheck.IsChecked = settings.CloseToTray;
            AlwaysTopCheck.IsChecked = settings.AlwaysOnTop;
            Topmost = settings.AlwaysOnTop;
            try { StartupCheck.IsChecked = StartupService.IsEnabled(); } catch { SettingsStatusText.Text = "자동 실행 설정을 읽을 수 없습니다."; }
            _gathering.Load(settings.GatheringAlarmsEnabled, settings.GatheringAlarmItems ?? new(), settings.GatheringVolumePercent);
            var bundled = Path.Combine(AppContext.BaseDirectory, "Assets", "Music");
            foreach (var path in MusicCatalog.StartupPlaylist(settings.MusicFiles, bundled))
                _musicFiles.Add(new MusicFileItem(path));

            foreach (var saved in settings.BuffRules)
            {
                var existing = _rules.FirstOrDefault(r => r.Name.Equals(saved.Name, StringComparison.OrdinalIgnoreCase));
                if (existing is not null)
                {
                    existing.AudioFile = string.IsNullOrWhiteSpace(saved.AudioFile) ? DefaultAlertAudio : saved.AudioFile;
                }
            }

            MusicList.SelectedItem = _musicFiles.FirstOrDefault();
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
            ErinClockCorrectionMilliseconds = ErinClock.CorrectionMilliseconds,
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
            GatheringAlarmsEnabled = _gathering.AlarmsEnabled,
            GatheringAlarmItems = _gathering.SelectedItems,
            GatheringVolumePercent = _gathering.VolumePercent,
            StartInBackground = StartBackgroundCheck.IsChecked == true,
            CloseToTray = CloseToTrayCheck.IsChecked == true,
            AlwaysOnTop = AlwaysTopCheck.IsChecked == true,
            UpdateFeedUrl = _updateFeedPath,
            BuffRules = _rules.Select(r => new SavedBuffRule
            {
                Name = r.Name,
                AudioFile = r.AudioFile,
            }).ToList()
        });
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
    private void ChooseAudio_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
    private void AddRule_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";

    private void RefreshRules() => RulesGrid.Items.Refresh();

    private void Start_Click(object sender, RoutedEventArgs e) => StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StatusText.Text = "넥슨정책상 해당 기능이 지원되지 않습니다.";
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
        GatheringHost.Visibility = Visibility.Collapsed;
        GatheringTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        BeadPanel.Visibility = Visibility.Collapsed;
        FeePanel.Visibility = Visibility.Collapsed;
        BeadTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        FeeTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        SettingsPanel.Visibility = Visibility.Collapsed;
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
        SettingsTabButton.Background = inactive;
        UpdateTabButton.Background = inactive;
    }
    private void GatheringTab_Click(object sender, RoutedEventArgs e)
    {
        SelectFeatureTab(false);
        BuffPanel.Visibility = Visibility.Collapsed;
        GatheringHost.Visibility = Visibility.Visible;
        GatheringTabButton.Background = BuffTabButton.Background;
        BuffTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
        _gathering.Refresh();
    }
    private void SettingsTab_Click(object sender, RoutedEventArgs e)
    {
        SelectFeatureTab(false);
        BuffPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
        SettingsTabButton.Background = BuffTabButton.Background;
        BuffTabButton.Background = (System.Windows.Media.Brush)FindResource("ButtonGradient");
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
                _auctionQuery = AuctionModeBox.SelectedIndex == 1 ? AuctionService.NormalizeQuery(AuctionQueryBox.Text) : _itemNames.ResolveName(AuctionQueryBox.Text);
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
            MinHeight = 106;
            Height = 106;
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
    private void UpdateErinClock()
    {
        var now = GameClockSync.Now;
        var minute = ErinClock.Minute(now);
        var weekday = ErinWeekday.At(now);
        ErinWeekdayText.Text = weekday.Label;
        ErinWeekdayText.ToolTip = weekday.Description;
        var ticker = weekday.Label + "   ·   " + weekday.Description.Split('\n', 2)[1].Replace("\n", "   ·   ");
        if (WeekdayTickerText.Text != ticker)
        {
            WeekdayTickerText.Text = ticker;
            RestartWeekdayTicker();
        }
        ErinPeriodText.Text = minute < 720 ? "AM" : "PM";
        var hour = minute / 60 % 12;
        ErinDigitalText.Text = $"{(hour == 0 ? 12 : hour):00} : {minute % 60:00}";
    }
    private void WeekdayTicker_SizeChanged(object sender, SizeChangedEventArgs e) => RestartWeekdayTicker();
    private void RestartWeekdayTicker()
    {
        if (WeekdayTickerText is null || WeekdayTickerViewport is null || WeekdayTickerViewport.ActualWidth <= 0) return;
        WeekdayTickerText.Measure(new Size(double.PositiveInfinity, 13));
        var distance = WeekdayTickerViewport.ActualWidth + WeekdayTickerText.DesiredSize.Width + 24;
        WeekdayTickerTransform.BeginAnimation(System.Windows.Media.TranslateTransform.XProperty,
            new System.Windows.Media.Animation.DoubleAnimation(WeekdayTickerViewport.ActualWidth,
                -WeekdayTickerText.DesiredSize.Width - 24, TimeSpan.FromSeconds(distance / 24))
            { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever });
    }
    private async Task SyncClockAsync()
    {
        try
        {
            await GameClockSync.SynchronizeAsync();
            if (_closed) return;
            _gathering.ResetClock();
            UpdateErinClock();
            ClockSyncStatus.Text = "서버 시각 동기화 완료 · 채널 차이는 인게임 시각으로 맞춰 주세요.";
        }
        catch { if (!_closed) ClockSyncStatus.Text = "서버 연결 실패 · PC 시각 및 저장된 보정값을 사용합니다."; }
    }
    private async void SyncClock_Click(object sender, RoutedEventArgs e) => await SyncClockAsync();
    private void AlignGameClock_Click(object sender, RoutedEventArgs e)
    {
        if (!DateTime.TryParseExact(GameClockInput.Text.Trim(), "HH:mm", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var time))
        { ClockSyncStatus.Text = "현재 게임 시각을 HH:mm으로 입력하세요. 예: 23:10"; return; }
        ErinClock.Align(GameClockSync.Now, time.Hour, time.Minute);
        _gathering.ResetClock();
        UpdateErinClock();
        SaveSettings();
        ClockSyncStatus.Text = "만돌린 7채널 기준으로 보정 저장 완료 · 채집 타이머·알람에도 적용됩니다.";
    }
    private void InitializeTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "블랙카드 도우미.exe");
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(iconPath);
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("블랙카드 도우미 열기", null, (_, _) => Dispatcher.Invoke(RestoreFromTray));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(() => { _allowExit = true; Close(); }));
        _tray = new System.Windows.Forms.NotifyIcon { Icon = icon is null ? System.Drawing.SystemIcons.Application : (System.Drawing.Icon)icon.Clone(), Text = "블랙카드 도우미", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(RestoreFromTray);
    }
    private void RestoreFromTray() { Show(); ShowInTaskbar = true; WindowState = WindowState.Normal; Activate(); }
    public void HideToTray()
    {
        if (_tray is null) { SettingsStatusText.Text = "트레이 아이콘을 준비할 수 없습니다."; return; }
        MusicPopup.IsOpen = AuctionSuggestionsPopup.IsOpen = false;
        ShowInTaskbar = false; Hide();
        _tray.ShowBalloonTip(2500, "블랙카드 도우미", "백그라운드에서 채집 알람이 계속 동작합니다. 트레이 아이콘을 두 번 클릭하면 열립니다.", System.Windows.Forms.ToolTipIcon.Info);
    }
    private void Background_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        try { StartupService.SetEnabled(StartupCheck.IsChecked == true, StartBackgroundCheck.IsChecked == true); SettingsStatusText.Text = StartupCheck.IsChecked == true ? "Windows 로그인 시 자동 실행합니다." : "자동 실행을 껐습니다."; }
        catch { _loadingSettings = true; StartupCheck.IsChecked = !StartupCheck.IsChecked; _loadingSettings = false; SettingsStatusText.Text = "자동 실행 설정을 저장하지 못했습니다."; }
    }
    private void Preferences_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingSettings) return;
        Topmost = AlwaysTopCheck.IsChecked == true;
        if (ReferenceEquals(sender, StartBackgroundCheck) && StartupCheck.IsChecked == true) Startup_Changed(StartupCheck, e);
        SaveSettings();
    }
    private void ShowClickRipple(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_closed || e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        var point = e.GetPosition(ClickEffectsCanvas);
        if (point.Y < 106 || point.X < 0 || point.Y > ClickEffectsCanvas.ActualHeight || point.X > ClickEffectsCanvas.ActualWidth) return;
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

    private void AddLog(string message)
    {
        StatusText.ToolTip = message;
    }
}

public sealed class MusicFileItem
{
    public string Path { get; }
    public string DisplayName => MusicCatalog.DisplayName(Path);
    public MusicFileItem(string path) => Path = path;
}











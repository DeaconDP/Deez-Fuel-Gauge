using System.Globalization;
using System.Diagnostics;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using DeezFuelGauge.ViewModels;

namespace DeezFuelGauge;

public partial class MainWindow : Window, ISettingsPanelHost
{
    private readonly UsageClient _usageClient = new();
    private readonly OpenAiBillingClient _openAiBilling = new();
    private readonly CodexUsageClient _codexBilling = new();
    private readonly AnthropicBillingClient _anthropicBilling = new();
    private readonly ClaudeProUsageClient _claudeProBilling = new();
    private readonly AntigravityUsageClient _antigravityBilling = new();
    private readonly OpenRouterUsageClient _openRouterBilling = new();
    private readonly OpenCodeUsageClient _openCodeBilling = new();
    private readonly DirectBillingService _directBilling;
    private readonly UsageRefreshService _refreshService;
    private readonly DebouncedAction _debouncedPositionSave;
    private readonly ProviderEasySetupService _easySetup;
    private readonly SettingsPanelViewModel _settingsViewModel;
    private readonly DispatcherTimer _pollTimer;
    private readonly WidgetSettings _settings;
    private bool _isRefreshing;
    private bool _isCodexLimitsExpanded;
    private bool _isClaudeProLimitsExpanded;
    private bool _isAntigravityLimitsExpanded;
    private bool _isSettingsExpanded;
    private bool _isCursorProviderExpanded;
    private bool _isOpenAiProviderExpanded;
    private bool _isClaudeProviderExpanded;
    private bool _isGeminiProviderExpanded;
    private bool _isOpenRouterProviderExpanded;
    private bool _isOpenCodeProviderExpanded;
    private bool _isOpenCodeGoLimitsExpanded;
    private double _lastPercentUsed;
    private double _lastAutoPercent;
    private double _lastApiPercent;
    private double _lastOpenAiPercent;
    private double _lastCodexSessionPercent;
    private double _lastCodexWeeklyPercent;
    private double _lastCodexPercent;
    private double _lastClaudePercent;
    private double _lastClaudeProSessionPercent;
    private double _lastClaudeProWeeklyPercent;
    private double _lastClaudeProPercent;
    private double _lastGeminiPercent;
    private double _lastOpenAiDirectPercent;
    private double _lastClaudeDirectPercent;
    private double _lastAntigravityGeminiSessionPercent;
    private double _lastAntigravityGeminiWeeklyPercent;
    private double _lastAntigravityThirdPartySessionPercent;
    private double _lastAntigravityThirdPartyWeeklyPercent;
    private double _lastAntigravityPercent;
    private double _lastCursorHeadlinePercent;
    private double _lastOpenAiHeadlinePercent;
    private double _lastClaudeHeadlinePercent;
    private double _lastGeminiHeadlinePercent;
    private double _lastOpenRouterPercent;
    private double _lastOpenRouterHeadlinePercent;
    private double _lastOpenCodeZenPercent;
    private double _lastOpenCodeGoPercent;
    private double _lastOpenCodeGoRollingPercent;
    private double _lastOpenCodeGoWeeklyPercent;
    private double _lastOpenCodeGoMonthlyPercent;
    private double _lastOpenCodeHeadlinePercent;
    private UsageSnapshot? _lastSnapshot;
    private readonly List<DiskBarRow> _diskBarRows = [];
    private readonly HardwareMetricsProvider _hardwareMetricsProvider = new();
    private readonly WidgetViewModel _widgetViewModel = new();
    private readonly List<HardwareBarRow> _hardwareBarRows = [];
    private readonly DispatcherTimer _hardwareTimer;
    private static readonly TimeSpan HardwareRefreshInterval = TimeSpan.FromSeconds(2);
    private int _hardwareSampleInFlight;
    private HardwareMetricsSnapshot? _lastAppliedHardwareSnapshot;
    private double _lastProgressLayoutWidth;
    private double _anchorFromHeight;
    private bool _pendingAnchorCompensation;

    private sealed class DiskBarRow
    {
        public required string Name { get; init; }
        public required StackPanel Container { get; init; }
        public required Grid Track { get; init; }
        public required Border Fill { get; init; }
        public required TextBlock Detail { get; init; }
        public double LastPercent { get; set; }
    }

    private sealed class HardwareBarRow
    {
        public required string Key { get; init; }
        public required StackPanel Container { get; init; }
        public required Grid BarGrid { get; init; }
        public required Grid Track { get; init; }
        public required Border Fill { get; init; }
        public required TextBlock Detail { get; init; }
        public double LastPercent { get; set; }
    }

    public MainWindow()
    {
        InitializeComponent();

        _directBilling = new DirectBillingService(
            _openAiBilling,
            _codexBilling,
            _anthropicBilling,
            _claudeProBilling,
            _antigravityBilling,
            _openRouterBilling,
            _openCodeBilling);
        _refreshService = new UsageRefreshService(_usageClient, _directBilling);
        _debouncedPositionSave = new DebouncedAction(SaveSettings, TimeSpan.FromMilliseconds(400));
        _easySetup = new ProviderEasySetupService(
            _codexBilling,
            _claudeProBilling,
            _antigravityBilling);
        _settingsViewModel = new SettingsPanelViewModel(
            _easySetup,
            _openAiBilling,
            _codexBilling,
            _antigravityBilling,
            _openRouterBilling,
            _openCodeBilling,
            anthropicBilling: _anthropicBilling,
            claudeProBilling: _claudeProBilling);

        SystemDecorations = SystemDecorations.None;

        _settings = SettingsStore.Load();
        _settingsViewModel.AttachHost(this);
        SettingsPanelControl.Initialize(_settingsViewModel, _settings);
        SyncSettingsAndVisibility();
        _isCodexLimitsExpanded = _settings.IsCodexLimitsExpanded;
        _isClaudeProLimitsExpanded = _settings.IsClaudeProLimitsExpanded;
        _isAntigravityLimitsExpanded = _settings.IsAntigravityLimitsExpanded;
        _isSettingsExpanded = _settings.IsSettingsExpanded;
        _isCursorProviderExpanded = _settings.IsCursorProviderExpanded;
        _isOpenAiProviderExpanded = _settings.IsOpenAiProviderExpanded;
        _isClaudeProviderExpanded = _settings.IsClaudeProviderExpanded;
        _isGeminiProviderExpanded = _settings.IsGeminiProviderExpanded;
        _isOpenRouterProviderExpanded = _settings.IsOpenRouterProviderExpanded;
        _isOpenCodeProviderExpanded = _settings.IsOpenCodeProviderExpanded;
        _isOpenCodeGoLimitsExpanded = _settings.IsOpenCodeGoLimitsExpanded;
        UpdateCodexLimitsExpandedState();
        UpdateClaudeProLimitsExpandedState();
        UpdateAntigravityLimitsExpandedState();
        UpdateOpenCodeGoLimitsExpandedState();
        UpdateSettingsExpandedState();
        UpdatePinIconState();
        UpdateAllProviderExpandedState();

        SettingsPanelHost.SizeChanged += (_, _) => CompensateAnchorIfNeeded();

        _pollTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(Math.Max(1, _settings.RefreshIntervalMinutes))
        };
        _pollTimer.Tick += async (_, _) => await RefreshAsync();
        _pollTimer.Start();

        _hardwareTimer = new DispatcherTimer { Interval = HardwareRefreshInterval };
        _hardwareTimer.Tick += async (_, _) => await SampleHardwareMetricsAsync();
        ApplyHardwareTimerState();

        Opened += async (_, _) =>
        {
            Dispatcher.UIThread.Post(ApplyInitialPosition, DispatcherPriority.Loaded);
            await RefreshAsync();
        };
        SizeChanged += (_, _) =>
        {
            UpdateAllProgressWidths();
            CompensateAnchorIfNeeded();
        };
        PositionChanged += (_, _) => _debouncedPositionSave.Invoke();
        Closing += (_, _) =>
        {
            _debouncedPositionSave.Flush();
            SaveSettings();
        };
    }

    private void ApplyInitialPosition()
    {
        if (_settings.IsPositionPinned)
        {
            Position = new PixelPoint((int)_settings.Left, (int)_settings.Top);
            return;
        }

        var screen = Screens.Primary;
        if (screen is null)
            return;

        var area = screen.WorkingArea;
        var width = (int)Math.Max(1, Bounds.Width);
        var height = (int)Math.Max(1, Bounds.Height);
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            area.X, area.Y, area.Width, area.Height, width, height);
        Position = new PixelPoint(x, y);
    }

    private void PinToggle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _settings.IsPositionPinned = !_settings.IsPositionPinned;
        if (_settings.IsPositionPinned)
        {
            _settings.Left = Position.X;
            _settings.Top = Position.Y;
        }

        UpdatePinIconState();
        SaveSettings();
        e.Handled = true;
    }

    private void RefreshButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        _ = RefreshAsync();
        e.Handled = true;
    }

    private void CloseButton_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Close();
        e.Handled = true;
    }

    private void UpdatePinIconState()
    {
        PinIcon.Opacity = _settings.IsPositionPinned ? 1 : 0.45;
        ToolTip.SetTip(PinButton, _settings.IsPositionPinned ? "Unpin" : "Pin");
    }

    private void SettingsToggle_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        var oldHeight = Bounds.Height;
        _isSettingsExpanded = !_isSettingsExpanded;
        UpdateSettingsExpandedState();
        ScheduleLayoutRefresh(oldHeight);
        SaveSettings();
        e.Handled = true;
    }

    private void UpdateSettingsExpandedState()
    {
        SettingsPanelHost.IsVisible = _isSettingsExpanded;
        SettingsIcon.Foreground = new SolidColorBrush(
            _isSettingsExpanded ? Color.FromRgb(0x88, 0xFF, 0x88) : Color.FromRgb(0x44, 0xCC, 0x44));
    }

    public void OnSettingsLayoutChanged()
    {
        if (!_isSettingsExpanded)
            return;

        ScheduleLayoutRefresh(Bounds.Height);
    }

    private void ScheduleLayoutRefresh(double anchorFromHeight)
    {
        _anchorFromHeight = anchorFromHeight;
        _pendingAnchorCompensation = true;
        RefreshWindowHeight();
        Dispatcher.UIThread.Post(() =>
        {
            CompensateAnchorIfNeeded();
            UpdateDynamicProgressWidths();
        }, DispatcherPriority.Loaded);
    }

    private void CompensateAnchorIfNeeded()
    {
        if (!_pendingAnchorCompensation)
            return;

        var newHeight = Bounds.Height;
        if (Math.Abs(newHeight - _anchorFromHeight) < 0.5)
            return;

        _pendingAnchorCompensation = false;
        Position = new PixelPoint(
            Position.X,
            WindowAnchorHelper.CompensateVerticalGrowth(_anchorFromHeight, newHeight, Position.Y));
    }

    public void OnSettingsChanged()
    {
        var oldHeight = Bounds.Height;
        SyncSettingsAndVisibility();
        SaveSettings();
        DiskSpaceProvider.InvalidateCache();

        if (_lastSnapshot is not null)
            ApplySnapshot(_lastSnapshot);

        RefreshDiskVolumes();
        ApplyHardwareTimerState();
        _ = SampleHardwareMetricsAsync();
        ScheduleLayoutRefresh(oldHeight);
    }

    public async Task OnEasySetupCompletedAsync()
    {
        SyncSettingsAndVisibility();
        SaveSettings();
        await RefreshAsync();
    }

    private void SyncSettingsAndVisibility()
    {
        SettingsPanelControl.CommitToSettings(_settings);
        ApplyProviderVisibility();
    }

    private void ApplyProviderVisibility()
    {
        CursorProviderSection.IsVisible = ProviderDashboardPresenter.IsCursorDashboardVisible(_settings);
        OpenAiProviderSection.IsVisible = ProviderDashboardPresenter.IsOpenAiDashboardVisible(_settings.OpenAi);
        ClaudeProviderSection.IsVisible = ProviderDashboardPresenter.IsClaudeDashboardVisible(_settings.Claude);
        GeminiProviderSection.IsVisible = ProviderDashboardPresenter.IsGeminiDashboardVisible(_settings.Gemini);
        OpenRouterProviderSection.IsVisible = ProviderDashboardPresenter.IsOpenRouterDashboardVisible(_settings.OpenRouter);
        OpenCodeProviderSection.IsVisible = ProviderDashboardPresenter.IsOpenCodeDashboardVisible(_settings.OpenCode);

        CursorSection.IsVisible = _settings.Cursor.ShowCursorSource;
        OpenAiSection.IsVisible = _settings.OpenAi.ShowCursorSource;
        OpenAiDirectSection.IsVisible = _settings.OpenAi.ShowDirectSource;
        CodexLimitsSection.IsVisible = _settings.OpenAi.ShowProLimits;
        ClaudeSection.IsVisible = _settings.Claude.ShowCursorSource;
        ClaudeDirectSection.IsVisible = _settings.Claude.ShowApiConsoleBilling;
        ClaudeProLimitsSection.IsVisible = _settings.Claude.ShowProLimits;
        GeminiSection.IsVisible = _settings.Gemini.ShowCursorSource;
        AntigravityLimitsSection.IsVisible = _settings.Gemini.ShowProLimits;
        OpenRouterLimitsSection.IsVisible = _settings.OpenRouter.ShowProLimits;
        OpenCodeZenSection.IsVisible = _settings.OpenCode.ShowDirectSource;
        OpenCodeGoLimitsSection.IsVisible = _settings.OpenCode.ShowProLimits;

        ApplyProviderDetailChrome();
    }

    private void ApplyProviderDetailChrome()
    {
        var cursorExpanded = _isCursorProviderExpanded;
        var openAiExpanded = _isOpenAiProviderExpanded;
        var claudeExpanded = _isClaudeProviderExpanded;
        var geminiExpanded = _isGeminiProviderExpanded;
        var openRouterExpanded = _isOpenRouterProviderExpanded;
        var openCodeExpanded = _isOpenCodeProviderExpanded;

        // Headline already shows aggregate plan usage — hide the duplicate nested Cursor bar.
        // Keep PercentText only for error messages when the Cursor section is expanded.
        var showCursorError = cursorExpanded
            && _lastSnapshot is { IsError: true }
            && ProviderDashboardPresenter.IsCursorDashboardVisible(_settings);
        PercentText.IsVisible = showCursorError;
        CursorBarBorder.IsVisible = false;
        RemainingText.IsVisible = false;
        if (cursorExpanded && _settings.Cursor.ShowCursorSource && !showCursorError)
            RefreshCursorBreakdownVisibility(_lastSnapshot ?? new UsageSnapshot());
        else if (!showCursorError)
            BreakdownPanel.IsVisible = false;

        OpenAiDetailText.IsVisible = cursorExpanded && _settings.OpenAi.ShowCursorSource && _settings.OpenAi.ShowDetails;
        OpenAiDirectDetailText.IsVisible = openAiExpanded && _settings.OpenAi.ShowDirectSource && _settings.OpenAi.EffectiveShowDirectDetails;
        CodexRemainingText.IsVisible = openAiExpanded && _settings.OpenAi.ShowProLimits && _settings.OpenAi.EffectiveShowProDetails;
        if (!openAiExpanded)
        {
            CodexBreakdownSection.IsVisible = false;
            CodexPercentText.IsVisible = false;
        }
        else
        {
            CodexPercentText.IsVisible = _settings.OpenAi.ShowProLimits;
        }

        ClaudeDetailText.IsVisible = cursorExpanded && _settings.Claude.ShowCursorSource && _settings.Claude.ShowDetails;
        ClaudeDirectDetailText.IsVisible = claudeExpanded && _settings.Claude.ShowApiConsoleBilling && _settings.Claude.EffectiveShowDirectDetails;
        ClaudeProRemainingText.IsVisible = claudeExpanded && _settings.Claude.ShowProLimits && _settings.Claude.EffectiveShowProDetails;
        if (!claudeExpanded)
        {
            ClaudeProBreakdownSection.IsVisible = false;
            ClaudeProPercentText.IsVisible = false;
        }
        else
        {
            ClaudeProPercentText.IsVisible = _settings.Claude.ShowProLimits;
        }

        GeminiDetailText.IsVisible = cursorExpanded && _settings.Gemini.ShowCursorSource && _settings.Gemini.ShowDetails;
        AntigravityRemainingText.IsVisible = geminiExpanded && _settings.Gemini.ShowProLimits && _settings.Gemini.EffectiveShowProDetails;
        if (!geminiExpanded)
        {
            AntigravityBreakdownSection.IsVisible = false;
            AntigravityPercentText.IsVisible = false;
        }
        else
        {
            AntigravityPercentText.IsVisible = _settings.Gemini.ShowProLimits;
        }

        OpenRouterDetailText.IsVisible = openRouterExpanded && _settings.OpenRouter.ShowProLimits && _settings.OpenRouter.ShowDetails;
        if (!openRouterExpanded)
            OpenRouterPercentText.IsVisible = false;
        else
            OpenRouterPercentText.IsVisible = _settings.OpenRouter.ShowProLimits;

        OpenCodeZenDetailText.IsVisible = openCodeExpanded && _settings.OpenCode.ShowDirectSource && _settings.OpenCode.ShowDetails;
        OpenCodeGoRemainingText.IsVisible = openCodeExpanded && _settings.OpenCode.ShowProLimits && _settings.OpenCode.EffectiveShowProDetails;
        if (!openCodeExpanded)
        {
            OpenCodeGoBreakdownSection.IsVisible = false;
            OpenCodeGoPercentText.IsVisible = false;
        }
        else
        {
            OpenCodeGoPercentText.IsVisible = _settings.OpenCode.ShowProLimits;
        }
    }

    private void OpenRouterProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.OpenRouter);
        SaveSettings();
        e.Handled = true;
    }

    private void OpenCodeProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.OpenCode);
        SaveSettings();
        e.Handled = true;
    }

    private void OpenCodeGoLimits_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!OpenCodeGoBreakdownSection.IsVisible)
            return;

        var oldHeight = Bounds.Height;
        _isOpenCodeGoLimitsExpanded = !_isOpenCodeGoLimitsExpanded;
        UpdateOpenCodeGoLimitsExpandedState();
        ScheduleLayoutRefresh(oldHeight);
        SaveSettings();
        e.Handled = true;
    }

    private void UpdateOpenCodeGoLimitsExpandedState()
    {
        OpenCodeGoBreakdownPanel.IsVisible = _isOpenCodeGoLimitsExpanded;
        OpenCodeGoBreakdownChevron.Text = _isOpenCodeGoLimitsExpanded ? "\u25B4" : "\u25BE";
    }

    private void CursorProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.Cursor);
        SaveSettings();
        e.Handled = true;
    }

    private void OpenAiProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.OpenAi);
        SaveSettings();
        e.Handled = true;
    }

    private void ClaudeProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.Claude);
        SaveSettings();
        e.Handled = true;
    }

    private void GeminiProvider_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        ToggleProviderExpanded(ProviderSection.Gemini);
        SaveSettings();
        e.Handled = true;
    }

    private void ToggleProviderExpanded(ProviderSection section)
    {
        var oldHeight = Bounds.Height;
        var state = ReadProviderExpandState();
        state = ProviderExpandPresenter.Toggle(section, state);
        WriteProviderExpandState(state);
        UpdateAllProviderExpandedState();
        ScheduleLayoutRefresh(oldHeight);
    }

    private ProviderExpandState ReadProviderExpandState() => new(
        _isCursorProviderExpanded,
        _isOpenAiProviderExpanded,
        _isClaudeProviderExpanded,
        _isGeminiProviderExpanded,
        _isOpenRouterProviderExpanded,
        _isOpenCodeProviderExpanded);

    private void WriteProviderExpandState(ProviderExpandState state)
    {
        _isCursorProviderExpanded = state.Cursor;
        _isOpenAiProviderExpanded = state.OpenAi;
        _isClaudeProviderExpanded = state.Claude;
        _isGeminiProviderExpanded = state.Gemini;
        _isOpenRouterProviderExpanded = state.OpenRouter;
        _isOpenCodeProviderExpanded = state.OpenCode;
    }

    private void UpdateAllProviderExpandedState()
    {
        UpdateCursorProviderExpandedState();
        UpdateOpenAiProviderExpandedState();
        UpdateClaudeProviderExpandedState();
        UpdateGeminiProviderExpandedState();
        UpdateOpenRouterProviderExpandedState();
        UpdateOpenCodeProviderExpandedState();
    }

    private void UpdateCursorProviderExpandedState()
    {
        CursorDetailsPanel.IsVisible = _isCursorProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isCursorProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
            RefreshCursorBreakdownVisibility(_lastSnapshot);
    }

    private void UpdateOpenAiProviderExpandedState()
    {
        OpenAiDetailsPanel.IsVisible = _isOpenAiProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isOpenAiProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
        {
            ApplyCodexLimitsBreakdownLayout(_settings.OpenAi, _lastSnapshot.Codex);
            UpdateCodexLimitsExpandedState();
            CodexPercentText.IsVisible = _settings.OpenAi.ShowProLimits;
        }
    }

    private void UpdateClaudeProviderExpandedState()
    {
        ClaudeDetailsPanel.IsVisible = _isClaudeProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isClaudeProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
        {
            ApplyClaudeProLimitsBreakdownLayout(_settings.Claude, _lastSnapshot.ClaudePro);
            UpdateClaudeProLimitsExpandedState();
            ClaudeProPercentText.IsVisible = _settings.Claude.ShowProLimits;
        }
    }

    private void UpdateGeminiProviderExpandedState()
    {
        GeminiDetailsPanel.IsVisible = _isGeminiProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isGeminiProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
        {
            ApplyAntigravityLimitsBreakdownLayout(_settings.Gemini, _lastSnapshot.Antigravity);
            UpdateAntigravityLimitsExpandedState();
            AntigravityPercentText.IsVisible = _settings.Gemini.ShowProLimits;
        }
    }

    private void UpdateOpenRouterProviderExpandedState()
    {
        OpenRouterDetailsPanel.IsVisible = _isOpenRouterProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isOpenRouterProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
            OpenRouterPercentText.IsVisible = _settings.OpenRouter.ShowProLimits;
    }

    private void UpdateOpenCodeProviderExpandedState()
    {
        OpenCodeDetailsPanel.IsVisible = _isOpenCodeProviderExpanded;
        ApplyProviderDetailChrome();

        if (_isOpenCodeProviderExpanded && _lastSnapshot is not null && !_lastSnapshot.IsError)
        {
            ApplyOpenCodeGoLimitsBreakdownLayout(_settings.OpenCode, _lastSnapshot.OpenCode);
            UpdateOpenCodeGoLimitsExpandedState();
            OpenCodeGoPercentText.IsVisible = _settings.OpenCode.ShowProLimits;
        }
    }

    private void RefreshCursorBreakdownVisibility(UsageSnapshot snapshot)
    {
        var showBreakdown = _settings.ShowBreakdown && snapshot.HasBreakdown;
        BreakdownPanel.IsVisible = showBreakdown;
        if (showBreakdown)
            UpdateBreakdownPanel(snapshot);
    }

    private void Window_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void UpdateBreakdownPanel(UsageSnapshot snapshot)
    {
        _lastAutoPercent = snapshot.AutoPercentUsed ?? 0;
        _lastApiPercent = snapshot.ApiPercentUsed ?? 0;
        var autoRounded = Math.Round(_lastAutoPercent);
        var apiRounded = Math.Round(_lastApiPercent);
        AutoPercentText.Text = $"{autoRounded}%";
        ApiPercentText.Text = $"{apiRounded}%";
        var apiPlanNote = CursorBreakdownPresenter.FormatApiPlanNote(snapshot.PlanLimitCents);
        ToolTip.SetTip(AutoBreakdownRow, "Additional usage beyond limits consumes API quota or on-demand spend.");
        ToolTip.SetTip(ApiBreakdownRow, apiPlanNote);
        UpdateBreakdownProgressWidths();
    }

    private void CodexLimits_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!CodexBreakdownSection.IsVisible)
            return;

        var oldHeight = Bounds.Height;
        _isCodexLimitsExpanded = !_isCodexLimitsExpanded;
        UpdateCodexLimitsExpandedState();
        ScheduleLayoutRefresh(oldHeight);
        SaveSettings();
        e.Handled = true;
    }

    private void ClaudeProLimits_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!ClaudeProBreakdownSection.IsVisible)
            return;

        var oldHeight = Bounds.Height;
        _isClaudeProLimitsExpanded = !_isClaudeProLimitsExpanded;
        UpdateClaudeProLimitsExpandedState();
        ScheduleLayoutRefresh(oldHeight);
        SaveSettings();
        e.Handled = true;
    }

    private void AntigravityLimits_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        if (!AntigravityBreakdownSection.IsVisible)
            return;

        var oldHeight = Bounds.Height;
        _isAntigravityLimitsExpanded = !_isAntigravityLimitsExpanded;
        UpdateAntigravityLimitsExpandedState();
        ScheduleLayoutRefresh(oldHeight);
        SaveSettings();
        e.Handled = true;
    }

    private void UpdateCodexLimitsExpandedState()
    {
        CodexBreakdownPanel.IsVisible = _isCodexLimitsExpanded;
        CodexBreakdownChevron.Text = _isCodexLimitsExpanded ? "\u25B4" : "\u25BE";
    }

    private void UpdateClaudeProLimitsExpandedState()
    {
        ClaudeProBreakdownPanel.IsVisible = _isClaudeProLimitsExpanded;
        ClaudeProBreakdownChevron.Text = _isClaudeProLimitsExpanded ? "\u25B4" : "\u25BE";
    }

    private void UpdateAntigravityLimitsExpandedState()
    {
        AntigravityBreakdownPanel.IsVisible = _isAntigravityLimitsExpanded;
        AntigravityBreakdownChevron.Text = _isAntigravityLimitsExpanded ? "\u25B4" : "\u25BE";
    }

    private void ApplyCodexLimitsBreakdownLayout(ProviderBillingSettings options, CodexSnapshot codex)
    {
        if (!options.ShowProLimits)
            return;

        var summary = codex.IsAvailable
            ? ProviderLimitsPresenter.FormatSessionWeeklySummary(codex.SessionPercentUsed, codex.WeeklyPercentUsed)
            : codex.StatusMessage ?? codex.DetailLabel;

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            options.ShowProBreakdown,
            codex.IsAvailable,
            _isCodexLimitsExpanded,
            summary,
            ProviderLimitsPresenter.FormatCodexFooter(codex),
            options.EffectiveShowProDetails,
            CodexBreakdownSummary,
            CodexBreakdownSection,
            CodexBreakdownPanel,
            CodexBreakdownChevron,
            CodexBarBorder,
            CodexRemainingText);
    }

    private void ApplyClaudeProLimitsBreakdownLayout(ProviderBillingSettings options, ClaudeProSnapshot pro)
    {
        if (!options.ShowProLimits)
            return;

        var summary = pro.IsAvailable
            ? ProviderLimitsPresenter.FormatSessionWeeklySummary(pro.SessionPercentUsed, pro.WeeklyPercentUsed)
            : pro.StatusMessage ?? pro.DetailLabel;

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            options.ShowProBreakdown,
            pro.IsAvailable,
            _isClaudeProLimitsExpanded,
            summary,
            ProviderLimitsPresenter.FormatClaudeProFooter(pro),
            options.EffectiveShowProDetails,
            ClaudeProBreakdownSummary,
            ClaudeProBreakdownSection,
            ClaudeProBreakdownPanel,
            ClaudeProBreakdownChevron,
            ClaudeProBarBorder,
            ClaudeProRemainingText);
    }

    private void ApplyAntigravityLimitsBreakdownLayout(ProviderBillingSettings options, AntigravitySnapshot antigravity)
    {
        if (!options.ShowProLimits)
            return;

        var summary = ProviderLimitsPresenter.FormatAntigravitySummary(antigravity);

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            options.ShowProBreakdown,
            antigravity.IsAvailable,
            _isAntigravityLimitsExpanded,
            summary,
            ProviderLimitsPresenter.FormatAntigravityFooter(antigravity),
            options.EffectiveShowProDetails,
            AntigravityBreakdownSummary,
            AntigravityBreakdownSection,
            AntigravityBreakdownPanel,
            AntigravityBreakdownChevron,
            AntigravityBarBorder,
            AntigravityRemainingText);
    }

    private async void RefreshMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    private void QuitMenuItem_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void DeacLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OpenUrl("https://deac.online");
        e.Handled = true;
    }

    private void WorldbuildLink_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        OpenUrl("https://worldbuild.io");
        e.Handled = true;
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
            return;

        _isRefreshing = true;
        try
        {
            SyncSettingsAndVisibility();
            var result = await Task.Run(() => _refreshService.RefreshAsync(_settings));
            _widgetViewModel.ApplyRefreshResult(result);
            ApplySnapshot(result.Snapshot);
            ApplyDegradedTooltips();
            SaveSettings();
            _settingsViewModel.UpdateCursorConnectionStatus();
            _settingsViewModel.UpdateStatusFromSettings(_settings);
            _settingsViewModel.InvalidateAuthDetectionCache();
            UpdateLastRefreshedLabel();
        }
        catch
        {
            ApplySnapshot(UsageSnapshot.Error("Can't fetch usage"));
        }
        finally
        {
            _isRefreshing = false;
        }

        RefreshDiskVolumes();
        ApplyHardwareTimerState();
        await SampleHardwareMetricsAsync();
    }

    private void RefreshDiskVolumes()
    {
        try
        {
            ApplyDiskVolumes(DiskSpaceProvider.GetVolumes());
        }
        catch
        {
            ApplyDiskVolumes([]);
        }
    }

    private bool IsAnyHardwareMetricEnabled() =>
        _settings.ShowCpuUsage
        || _settings.ShowGpuUsage
        || _settings.ShowRamUsage
        || _settings.ShowCpuTemp;

    private void ApplyHardwareTimerState()
    {
        if (IsAnyHardwareMetricEnabled())
        {
            if (!_hardwareTimer.IsEnabled)
            {
                _hardwareMetricsProvider.ResetCpuBaseline();
                _lastAppliedHardwareSnapshot = null;
                // Show rows immediately so a slow first GPU sample can't hide CPU/RAM.
                ApplyHardwareMetrics(new HardwareMetricsSnapshot(), structureChanged: true);
                _hardwareTimer.Start();
                _ = SampleHardwareMetricsAsync();
            }

            return;
        }

        if (_hardwareTimer.IsEnabled)
            _hardwareTimer.Stop();

        if (_lastAppliedHardwareSnapshot is not null || _hardwareBarRows.Count > 0)
        {
            _lastAppliedHardwareSnapshot = null;
            ApplyHardwareMetrics(null, structureChanged: true);
        }
    }

    private async Task SampleHardwareMetricsAsync()
    {
        if (!IsAnyHardwareMetricEnabled())
        {
            if (_lastAppliedHardwareSnapshot is not null || _hardwareBarRows.Count > 0)
            {
                _lastAppliedHardwareSnapshot = null;
                ApplyHardwareMetrics(null, structureChanged: true);
            }

            return;
        }

        if (Interlocked.CompareExchange(ref _hardwareSampleInFlight, 1, 0) != 0)
            return;

        try
        {
            var snapshot = await Task.Run(() => _hardwareMetricsProvider.Sample());
            if (HardwareSnapshotUnchanged(_lastAppliedHardwareSnapshot, snapshot))
                return;

            _lastAppliedHardwareSnapshot = snapshot;
            ApplyHardwareMetrics(snapshot, structureChanged: false);
        }
        catch
        {
            // Keep placeholder rows visible when sampling fails.
            if (_hardwareBarRows.Count == 0)
                ApplyHardwareMetrics(new HardwareMetricsSnapshot(), structureChanged: true);
        }
        finally
        {
            Interlocked.Exchange(ref _hardwareSampleInFlight, 0);
        }
    }

    private static bool HardwareSnapshotUnchanged(
        HardwareMetricsSnapshot? previous,
        HardwareMetricsSnapshot current)
    {
        if (previous is null)
            return false;

        return previous.IsGpuAvailable == current.IsGpuAvailable
               && previous.IsCpuTempAvailable == current.IsCpuTempAvailable
               && RoundedPercent(previous.CpuPercent) == RoundedPercent(current.CpuPercent)
               && RoundedPercent(previous.GpuPercent) == RoundedPercent(current.GpuPercent)
               && RoundedPercent(previous.RamPercent) == RoundedPercent(current.RamPercent)
               && RoundedPercent(previous.CpuTempCelsius) == RoundedPercent(current.CpuTempCelsius);
    }

    private static double? RoundedPercent(double? value) =>
        value is null ? null : Math.Round(value.Value);

    private void ApplyHardwareMetrics(HardwareMetricsSnapshot? snapshot, bool structureChanged)
    {
        var oldHeight = Bounds.Height;
        var showCpuRow = _settings.ShowCpuUsage || ShouldShowCpuTempDetail();
        var showGpuRow = _settings.ShowGpuUsage && snapshot?.IsGpuAvailable == true;
        var showRamRow = _settings.ShowRamUsage;

        if (!showCpuRow && !showGpuRow && !showRamRow)
        {
            HardwareSection.IsVisible = false;
            if (_hardwareBarRows.Count > 0)
            {
                ClearHardwareBarRows();
                structureChanged = true;
            }

            if (structureChanged)
                ScheduleLayoutRefresh(oldHeight);

            return;
        }

        HardwareSection.IsVisible = true;
        var desiredKeys = new List<string>();
        if (showCpuRow)
            desiredKeys.Add("cpu");
        if (showGpuRow)
            desiredKeys.Add("gpu");
        if (showRamRow)
            desiredKeys.Add("ram");

        var rowCountBefore = _hardwareBarRows.Count;
        for (var i = _hardwareBarRows.Count - 1; i >= 0; i--)
        {
            if (!desiredKeys.Contains(_hardwareBarRows[i].Key, StringComparer.Ordinal))
            {
                HardwareSection.Children.Remove(_hardwareBarRows[i].Container);
                _hardwareBarRows.RemoveAt(i);
                structureChanged = true;
            }
        }

        if (showCpuRow)
            ApplyHardwareBarRow(GetOrCreateHardwareRow("cpu", "CPU"), snapshot, isCpu: true);
        if (showGpuRow)
            ApplyHardwareBarRow(GetOrCreateHardwareRow("gpu", "GPU"), snapshot, isCpu: false);
        if (showRamRow)
            ApplyHardwareBarRow(GetOrCreateHardwareRow("ram", "RAM"), snapshot, isCpu: false);

        if (_hardwareBarRows.Count != rowCountBefore)
            structureChanged = true;

        ReorderHardwareBarRows(desiredKeys);
        UpdateHardwareProgressWidths();
        if (structureChanged)
            ScheduleLayoutRefresh(oldHeight);
    }

    private HardwareBarRow GetOrCreateHardwareRow(string key, string label)
    {
        var row = _hardwareBarRows.FirstOrDefault(r =>
            string.Equals(r.Key, key, StringComparison.Ordinal));
        if (row is not null)
            return row;

        row = CreateHardwareBarRow(key, label);
        _hardwareBarRows.Add(row);
        HardwareSection.Children.Add(row.Container);
        return row;
    }

    private void ReorderHardwareBarRows(IReadOnlyList<string> keys)
    {
        foreach (var row in _hardwareBarRows)
            HardwareSection.Children.Remove(row.Container);

        foreach (var key in keys)
        {
            var row = _hardwareBarRows.First(r => string.Equals(r.Key, key, StringComparison.Ordinal));
            HardwareSection.Children.Add(row.Container);
        }
    }

    private bool ShouldShowCpuTempDetail() =>
        _settings.ShowCpuTemp && _settings.ShowCpuTempDetail;

    private void ApplyHardwareBarRow(HardwareBarRow row, HardwareMetricsSnapshot? snapshot, bool isCpu)
    {
        if (isCpu)
        {
            row.BarGrid.IsVisible = true;
            row.Track.IsVisible = _settings.ShowCpuUsage;
            row.Fill.IsVisible = _settings.ShowCpuUsage;

            if (!_settings.ShowCpuUsage)
            {
                row.LastPercent = 0;
                row.Fill.Width = 0;
                row.Detail.Text = ShouldShowCpuTempDetail()
                    ? HardwareMetricsSnapshot.FormatCpuTemp(snapshot?.CpuTempCelsius)
                    : "";
                row.Detail.IsVisible = ShouldShowCpuTempDetail();
                return;
            }

            var cpuPercent = snapshot?.CpuPercent;
            var isAvailable = cpuPercent is not null;
            var percent = cpuPercent ?? 0;
            var detail = BuildCpuDetail(snapshot);
            var lastPercent = row.LastPercent;
            ProviderBarPresenter.ApplyUsageBar(
                row.Track,
                row.Fill,
                ref lastPercent,
                percent,
                isAvailable,
                "—",
                detail,
                ShouldShowCpuTempDetail(),
                row.Detail);
            row.LastPercent = lastPercent;
            return;
        }

        if (string.Equals(row.Key, "gpu", StringComparison.Ordinal))
        {
            var gpuPercent = snapshot?.GpuPercent;
            var isAvailable = gpuPercent is not null;
            var percent = gpuPercent ?? 0;
            var lastPercent = row.LastPercent;
            ProviderBarPresenter.ApplyUsageBar(
                row.Track,
                row.Fill,
                ref lastPercent,
                percent,
                isAvailable,
                "—",
                "",
                false,
                row.Detail);
            row.LastPercent = lastPercent;
            return;
        }

        var ramPercent = snapshot?.RamPercent ?? 0;
        var ramDetail = snapshot is not null && _settings.ShowHardwareDetails
            ? HardwareMetricsSnapshot.FormatRamDetail(snapshot.RamUsedBytes, snapshot.RamTotalBytes)
            : "";
        var ramLastPercent = row.LastPercent;
        ProviderBarPresenter.ApplyUsageBar(
            row.Track,
            row.Fill,
            ref ramLastPercent,
            ramPercent,
            snapshot is not null,
            "—",
            ramDetail,
            _settings.ShowHardwareDetails,
            row.Detail);
        row.LastPercent = ramLastPercent;
    }

    private string BuildCpuDetail(HardwareMetricsSnapshot? snapshot)
    {
        var parts = new List<string>();
        if (ShouldShowCpuTempDetail())
            parts.Add(HardwareMetricsSnapshot.FormatCpuTemp(snapshot?.CpuTempCelsius));

        return string.Join(" · ", parts.Where(part => part.Length > 0));
    }

    private HardwareBarRow CreateHardwareBarRow(string key, string label)
    {
        var labelBlock = new TextBlock
        {
            Text = label,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontFamily = new FontFamily("Segoe UI Semibold, Segoe UI, sans-serif"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            MaxWidth = 52,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var fill = new Border
        {
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Width = 0,
            Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
        };

        var track = new Grid
        {
            Height = 6,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new Border { CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) },
                fill
            }
        };

        var barGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Children = { labelBlock, track }
        };
        Grid.SetColumn(labelBlock, 0);
        Grid.SetColumn(track, 1);

        var detail = new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 4),
            FontSize = 9,
            LineHeight = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
            IsVisible = false
        };

        var container = new StackPanel
        {
            Spacing = 0,
            Children = { barGrid, detail }
        };

        return new HardwareBarRow
        {
            Key = key,
            Container = container,
            BarGrid = barGrid,
            Track = track,
            Fill = fill,
            Detail = detail
        };
    }

    private void ClearHardwareBarRows()
    {
        HardwareSection.Children.Clear();
        _hardwareBarRows.Clear();
    }

    private void UpdateHardwareProgressWidths()
    {
        foreach (var row in _hardwareBarRows)
            ProviderBarPresenter.UpdateProgressWidth(row.Track, row.Fill, row.LastPercent);
    }

    private void ApplySnapshot(UsageSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        if (snapshot.IsError)
        {
            var oldHeight = Bounds.Height;
            PercentText.Text = snapshot.ErrorMessage ?? "Error";
            RemainingText.Text = "";
            ProgressFill.Width = 0;
            ProgressFill.Background = UsageBarBrushes.GetBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            PercentText.Foreground = UsageBarBrushes.GetBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            BreakdownPanel.IsVisible = false;
            ResetProviderBars();
            ApplyHeadlineBar(CursorHeadlineTrack, CursorHeadlineFill, ref _lastCursorHeadlinePercent, 0, showReadySliver: false);
            CursorHeadlineFill.Background = UsageBarBrushes.GetBrush(Color.FromRgb(0xFF, 0x98, 0x00));
            ProviderBarPresenter.ApplyReadyGlow(CursorHeadlineFill, active: false);
            CursorHeadlineTrack.Opacity = 0.45;
            if (ProviderDashboardPresenter.IsCursorDashboardVisible(_settings))
            {
                WriteProviderExpandState(ProviderExpandState.ExpandOnly(ProviderSection.Cursor));
                UpdateAllProviderExpandedState();
            }
            OpenAiProviderSection.IsVisible = false;
            ClaudeProviderSection.IsVisible = false;
            GeminiProviderSection.IsVisible = false;
            OpenRouterProviderSection.IsVisible = false;
            OpenCodeProviderSection.IsVisible = false;
            ScheduleLayoutRefresh(oldHeight);
            return;
        }

        _lastPercentUsed = snapshot.PercentUsed;
        var percent = Math.Round(snapshot.PercentUsed);
        PercentText.Text = $"{percent}% used";
        RemainingText.Text = _settings.Cursor.ShowDetails ? snapshot.RemainingLabel : "";
        ApplyProviderDetailChrome();
        UpdateProgressWidth(snapshot.PercentUsed);

        var accent = UsageBarColors.GetColorForPercent(snapshot.PercentUsed);
        ProgressFill.Background = UsageBarBrushes.GetBrush(accent);
        PercentText.Foreground = UsageBarBrushes.GetBrush(accent);

        var showBreakdown = _settings.ShowBreakdown && snapshot.HasBreakdown && _isCursorProviderExpanded;
        if (showBreakdown)
        {
            BreakdownPanel.IsVisible = true;
            UpdateBreakdownPanel(snapshot);
        }
        else
        {
            BreakdownPanel.IsVisible = false;
        }

        ApplyProviderBar(snapshot.OpenAi, _settings.OpenAi, ref _lastOpenAiPercent, OpenAiProgressTrack, OpenAiProgressFill, OpenAiDetailText);
        ApplyProviderBar(snapshot.Claude, _settings.Claude, ref _lastClaudePercent, ClaudeProgressTrack, ClaudeProgressFill, ClaudeDetailText);
        ApplyProviderBar(snapshot.Gemini, _settings.Gemini, ref _lastGeminiPercent, GeminiProgressTrack, GeminiProgressFill, GeminiDetailText);

        ApplyDirectProviderBar(snapshot.OpenAiDirect, _settings.OpenAi.ShowDirectSource, _settings.OpenAi.EffectiveShowDirectDetails, ref _lastOpenAiDirectPercent, OpenAiDirectProgressTrack, OpenAiDirectProgressFill, OpenAiDirectDetailText);
        ApplyCodexBars(snapshot.Codex, _settings.OpenAi);
        ApplyDirectProviderBar(snapshot.ClaudeDirect, _settings.Claude.ShowApiConsoleBilling, _settings.Claude.EffectiveShowDirectDetails, ref _lastClaudeDirectPercent, ClaudeDirectProgressTrack, ClaudeDirectProgressFill, ClaudeDirectDetailText);
        ApplyClaudeProBars(snapshot.ClaudePro, _settings.Claude);
        ApplyAntigravityBars(snapshot.Antigravity, _settings.Gemini);
        ApplyOpenRouterBars(snapshot.OpenRouter, _settings.OpenRouter);
        ApplyOpenCodeBars(snapshot.OpenCode, _settings.OpenCode);
        ApplyProviderHeadlines(snapshot);
        SyncSettingsAndVisibility();
    }

    private void ApplyProviderHeadlines(UsageSnapshot snapshot)
    {
        var cursorPercent = ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, _settings);
        ApplyHeadlineBar(
            CursorHeadlineTrack,
            CursorHeadlineFill,
            ref _lastCursorHeadlinePercent,
            cursorPercent,
            ProviderDashboardPresenter.IsCursorHeadlineConnected(snapshot, _settings) && cursorPercent <= 0);

        var openAiPercent = ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, _settings.OpenAi);
        ApplyHeadlineBar(
            OpenAiHeadlineTrack,
            OpenAiHeadlineFill,
            ref _lastOpenAiHeadlinePercent,
            openAiPercent,
            ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, _settings.OpenAi) && openAiPercent <= 0);

        var claudePercent = ProviderDashboardPresenter.ComputeClaudeHeadline(snapshot, _settings.Claude);
        ApplyHeadlineBar(
            ClaudeHeadlineTrack,
            ClaudeHeadlineFill,
            ref _lastClaudeHeadlinePercent,
            claudePercent,
            ProviderDashboardPresenter.IsClaudeHeadlineConnected(snapshot, _settings.Claude) && claudePercent <= 0);

        var geminiPercent = ProviderDashboardPresenter.ComputeGeminiHeadline(snapshot, _settings.Gemini);
        ApplyHeadlineBar(
            GeminiHeadlineTrack,
            GeminiHeadlineFill,
            ref _lastGeminiHeadlinePercent,
            geminiPercent,
            ProviderDashboardPresenter.IsGeminiHeadlineConnected(snapshot, _settings.Gemini) && geminiPercent <= 0);

        var openRouterPercent = ProviderDashboardPresenter.ComputeOpenRouterHeadline(snapshot, _settings.OpenRouter);
        ApplyHeadlineBar(
            OpenRouterHeadlineTrack,
            OpenRouterHeadlineFill,
            ref _lastOpenRouterHeadlinePercent,
            openRouterPercent,
            ProviderDashboardPresenter.IsOpenRouterHeadlineConnected(snapshot, _settings.OpenRouter) && openRouterPercent <= 0);

        var openCodePercent = ProviderDashboardPresenter.ComputeOpenCodeHeadline(snapshot, _settings.OpenCode);
        ApplyHeadlineBar(
            OpenCodeHeadlineTrack,
            OpenCodeHeadlineFill,
            ref _lastOpenCodeHeadlinePercent,
            openCodePercent,
            ProviderDashboardPresenter.IsOpenCodeHeadlineConnected(snapshot, _settings.OpenCode) && openCodePercent <= 0);
    }

    private static void ApplyHeadlineBar(Grid track, Border fill, ref double lastPercent, double percent, bool showReadySliver)
    {
        lastPercent = percent;
        track.Opacity = 1;
        fill.Background = UsageBarBrushes.GetBrushForPercent(percent);
        ProviderBarPresenter.SetReadySliverState(fill, showReadySliver);
        ProviderBarPresenter.UpdateProgressWidth(track, fill, percent);
    }

    private void ApplyProviderBar(
        ProviderUsageSnapshot provider,
        ProviderBillingSettings options,
        ref double lastPercent,
        Grid track,
        Border fill,
        TextBlock detailText)
    {
        if (!options.ShowCursorSource)
        {
            detailText.IsVisible = false;
            return;
        }

        ProviderBarPresenter.ApplyUsageBar(
            track,
            fill,
            ref lastPercent,
            provider.PercentUsed,
            provider.IsAvailable,
            provider.StatusMessage,
            provider.DetailLabel,
            options.ShowDetails,
            detailText);
    }

    private void ApplyCodexBars(CodexSnapshot codex, ProviderBillingSettings options)
    {
        if (!options.ShowProLimits)
            return;

        var headline = ProviderLimitsPresenter.HeadlinePercent(codex.SessionPercentUsed, codex.WeeklyPercentUsed);
        ProviderLimitsPresenter.ApplyHeadline(
            headline,
            codex.IsAvailable,
            codex.StatusMessage,
            CodexPercentText,
            CodexProgressTrack,
            CodexProgressFill,
            ref _lastCodexPercent);

        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            CodexSessionProgressTrack,
            CodexSessionProgressFill,
            CodexSessionPercentText,
            ref _lastCodexSessionPercent,
            codex.SessionPercentUsed,
            codex.IsAvailable);
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            CodexWeeklyProgressTrack,
            CodexWeeklyProgressFill,
            CodexWeeklyPercentText,
            ref _lastCodexWeeklyPercent,
            codex.WeeklyPercentUsed,
            codex.IsAvailable);

        ApplyCodexLimitsBreakdownLayout(options, codex);
        UpdateCodexLimitsExpandedState();
    }

    private void ApplyClaudeProBars(ClaudeProSnapshot pro, ProviderBillingSettings options)
    {
        if (!options.ShowProLimits)
            return;

        var headline = ProviderLimitsPresenter.HeadlinePercent(pro.SessionPercentUsed, pro.WeeklyPercentUsed);
        ProviderLimitsPresenter.ApplyHeadline(
            headline,
            pro.IsAvailable,
            pro.StatusMessage,
            ClaudeProPercentText,
            ClaudeProProgressTrack,
            ClaudeProProgressFill,
            ref _lastClaudeProPercent);

        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            ClaudeProSessionProgressTrack,
            ClaudeProSessionProgressFill,
            ClaudeProSessionPercentText,
            ref _lastClaudeProSessionPercent,
            pro.SessionPercentUsed,
            pro.IsAvailable);
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            ClaudeProWeeklyProgressTrack,
            ClaudeProWeeklyProgressFill,
            ClaudeProWeeklyPercentText,
            ref _lastClaudeProWeeklyPercent,
            pro.WeeklyPercentUsed,
            pro.IsAvailable);

        ApplyClaudeProLimitsBreakdownLayout(options, pro);
        UpdateClaudeProLimitsExpandedState();
    }

    private void ApplyAntigravityBars(AntigravitySnapshot antigravity, ProviderBillingSettings options)
    {
        if (!options.ShowProLimits)
            return;

        var headline = ProviderLimitsPresenter.AntigravityHeadlinePercent(antigravity);
        ProviderLimitsPresenter.ApplyHeadline(
            headline,
            antigravity.IsAvailable,
            antigravity.StatusMessage,
            AntigravityPercentText,
            AntigravityProgressTrack,
            AntigravityProgressFill,
            ref _lastAntigravityPercent);

        ApplyAntigravityGroupBar(
            antigravity.Gemini,
            ref _lastAntigravityGeminiSessionPercent,
            ref _lastAntigravityGeminiWeeklyPercent,
            AntigravityGeminiSessionProgressTrack,
            AntigravityGeminiSessionProgressFill,
            AntigravityGeminiSessionPercentText,
            AntigravityGeminiWeeklyProgressTrack,
            AntigravityGeminiWeeklyProgressFill,
            AntigravityGeminiWeeklyPercentText);

        ApplyAntigravityGroupBar(
            antigravity.ThirdParty,
            ref _lastAntigravityThirdPartySessionPercent,
            ref _lastAntigravityThirdPartyWeeklyPercent,
            AntigravityThirdPartySessionProgressTrack,
            AntigravityThirdPartySessionProgressFill,
            AntigravityThirdPartySessionPercentText,
            AntigravityThirdPartyWeeklyProgressTrack,
            AntigravityThirdPartyWeeklyProgressFill,
            AntigravityThirdPartyWeeklyPercentText);

        ApplyAntigravityLimitsBreakdownLayout(options, antigravity);
        UpdateAntigravityLimitsExpandedState();
    }

    private void ApplyOpenRouterBars(OpenRouterSnapshot openRouter, ProviderBillingSettings options)
    {
        if (!options.ShowProLimits)
            return;

        ProviderLimitsPresenter.ApplyHeadline(
            openRouter.HeadlinePercentUsed,
            openRouter.IsAvailable,
            openRouter.StatusMessage,
            OpenRouterPercentText,
            OpenRouterProgressTrack,
            OpenRouterProgressFill,
            ref _lastOpenRouterPercent);

        OpenRouterDetailText.Text = options.ShowDetails ? openRouter.DetailLabel : "";
        OpenRouterDetailText.IsVisible = options.ShowDetails && openRouter.IsAvailable;
    }

    private void ApplyOpenCodeBars(OpenCodeSnapshot openCode, ProviderBillingSettings options)
    {
        if (options.ShowDirectSource)
        {
            var zenPercent = openCode.ZenMonthlyPercentUsed
                ?? (openCode.ZenBalanceUsd is { } balance
                    ? balance <= 1 ? 95 : balance <= 5 ? 75 : balance <= 10 ? 50 : 10
                    : 0);

            ProviderBarPresenter.ApplyUsageBar(
                OpenCodeZenProgressTrack,
                OpenCodeZenProgressFill,
                ref _lastOpenCodeZenPercent,
                zenPercent,
                openCode.ZenIsAvailable,
                openCode.StatusMessage,
                openCode.DetailLabel,
                options.ShowDetails,
                OpenCodeZenDetailText);
        }

        if (!options.ShowProLimits || !openCode.HasGoSubscription)
            return;

        var headline = ProviderLimitsPresenter.HeadlinePercent3(
            openCode.GoRolling.PercentUsed,
            openCode.GoWeekly.PercentUsed,
            openCode.GoMonthly.PercentUsed);

        ProviderLimitsPresenter.ApplyHeadline(
            headline,
            openCode.GoRolling.IsAvailable || openCode.GoWeekly.IsAvailable || openCode.GoMonthly.IsAvailable,
            openCode.StatusMessage,
            OpenCodeGoPercentText,
            OpenCodeGoProgressTrack,
            OpenCodeGoProgressFill,
            ref _lastOpenCodeGoPercent);

        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            OpenCodeGoRollingProgressTrack,
            OpenCodeGoRollingProgressFill,
            OpenCodeGoRollingPercentText,
            ref _lastOpenCodeGoRollingPercent,
            openCode.GoRolling.PercentUsed,
            openCode.GoRolling.IsAvailable);
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            OpenCodeGoWeeklyProgressTrack,
            OpenCodeGoWeeklyProgressFill,
            OpenCodeGoWeeklyPercentText,
            ref _lastOpenCodeGoWeeklyPercent,
            openCode.GoWeekly.PercentUsed,
            openCode.GoWeekly.IsAvailable);
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            OpenCodeGoMonthlyProgressTrack,
            OpenCodeGoMonthlyProgressFill,
            OpenCodeGoMonthlyPercentText,
            ref _lastOpenCodeGoMonthlyPercent,
            openCode.GoMonthly.PercentUsed,
            openCode.GoMonthly.IsAvailable);

        ApplyOpenCodeGoLimitsBreakdownLayout(options, openCode);
        UpdateOpenCodeGoLimitsExpandedState();
    }

    private void ApplyOpenCodeGoLimitsBreakdownLayout(ProviderBillingSettings options, OpenCodeSnapshot openCode)
    {
        var showBreakdown = options.ShowProBreakdown &&
                            openCode.HasGoSubscription &&
                            (openCode.GoRolling.IsAvailable || openCode.GoWeekly.IsAvailable || openCode.GoMonthly.IsAvailable);
        var summary = ProviderLimitsPresenter.FormatThreeWindowSummary(
            openCode.GoRolling.PercentUsed,
            openCode.GoWeekly.PercentUsed,
            openCode.GoMonthly.PercentUsed);
        var footer = ProviderLimitsPresenter.FormatOpenCodeGoFooter(openCode);

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            showBreakdown,
            openCode.HasGoSubscription,
            _isOpenCodeGoLimitsExpanded,
            summary,
            footer,
            options.EffectiveShowProDetails,
            OpenCodeGoBreakdownSummary,
            OpenCodeGoBreakdownSection,
            OpenCodeGoBreakdownPanel,
            OpenCodeGoBreakdownChevron,
            OpenCodeGoBarBorder,
            OpenCodeGoRemainingText);
    }

    private static void ApplyAntigravityGroupBar(
        AntigravityGroupSnapshot group,
        ref double lastSessionPercent,
        ref double lastWeeklyPercent,
        Grid sessionTrack,
        Border sessionFill,
        TextBlock sessionPercentText,
        Grid weeklyTrack,
        Border weeklyFill,
        TextBlock weeklyPercentText)
    {
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            sessionTrack,
            sessionFill,
            sessionPercentText,
            ref lastSessionPercent,
            group.SessionPercentUsed,
            group.IsAvailable);
        ProviderLimitsPresenter.ApplyBreakdownSubBar(
            weeklyTrack,
            weeklyFill,
            weeklyPercentText,
            ref lastWeeklyPercent,
            group.WeeklyPercentUsed,
            group.IsAvailable);
    }

    private void ApplyDirectProviderBar(
        DirectProviderSnapshot provider,
        bool showBar,
        bool showDetails,
        ref double lastPercent,
        Grid track,
        Border fill,
        TextBlock detailText)
    {
        if (!showBar)
        {
            detailText.IsVisible = false;
            return;
        }

        ProviderBarPresenter.ApplyUsageBar(
            track,
            fill,
            ref lastPercent,
            provider.PercentUsed,
            provider.IsAvailable,
            provider.StatusMessage,
            provider.DetailLabel,
            showDetails,
            detailText);
    }

    private void ResetProviderBars()
    {
        _lastOpenAiPercent = 0;
        _lastCodexSessionPercent = 0;
        _lastCodexWeeklyPercent = 0;
        _lastCodexPercent = 0;
        _lastClaudePercent = 0;
        _lastClaudeProSessionPercent = 0;
        _lastClaudeProWeeklyPercent = 0;
        _lastClaudeProPercent = 0;
        _lastGeminiPercent = 0;
        _lastOpenAiDirectPercent = 0;
        _lastClaudeDirectPercent = 0;
        _lastAntigravityGeminiSessionPercent = 0;
        _lastAntigravityGeminiWeeklyPercent = 0;
        _lastAntigravityThirdPartySessionPercent = 0;
        _lastAntigravityThirdPartyWeeklyPercent = 0;
        _lastAntigravityPercent = 0;
        _lastOpenRouterPercent = 0;
        _lastOpenRouterHeadlinePercent = 0;
        _lastOpenCodeZenPercent = 0;
        _lastOpenCodeGoPercent = 0;
        _lastOpenCodeGoRollingPercent = 0;
        _lastOpenCodeGoWeeklyPercent = 0;
        _lastOpenCodeGoMonthlyPercent = 0;
        _lastOpenCodeHeadlinePercent = 0;
        _lastCursorHeadlinePercent = 0;
        _lastOpenAiHeadlinePercent = 0;
        _lastClaudeHeadlinePercent = 0;
        _lastGeminiHeadlinePercent = 0;
        OpenAiProgressFill.Width = 0;
        CodexProgressFill.Width = 0;
        CodexSessionProgressFill.Width = 0;
        CodexWeeklyProgressFill.Width = 0;
        ClaudeProgressFill.Width = 0;
        ClaudeProProgressFill.Width = 0;
        ClaudeProSessionProgressFill.Width = 0;
        ClaudeProWeeklyProgressFill.Width = 0;
        GeminiProgressFill.Width = 0;
        OpenAiDirectProgressFill.Width = 0;
        ClaudeDirectProgressFill.Width = 0;
        AntigravityProgressFill.Width = 0;
        AntigravityGeminiSessionProgressFill.Width = 0;
        AntigravityGeminiWeeklyProgressFill.Width = 0;
        AntigravityThirdPartySessionProgressFill.Width = 0;
        AntigravityThirdPartyWeeklyProgressFill.Width = 0;
        OpenRouterProgressFill.Width = 0;
        OpenCodeZenProgressFill.Width = 0;
        OpenCodeGoProgressFill.Width = 0;
        OpenCodeGoRollingProgressFill.Width = 0;
        OpenCodeGoWeeklyProgressFill.Width = 0;
        OpenCodeGoMonthlyProgressFill.Width = 0;
        OpenAiProgressTrack.Opacity = 0.45;
        CodexProgressTrack.Opacity = 0.45;
        CodexSessionProgressTrack.Opacity = 0.45;
        CodexWeeklyProgressTrack.Opacity = 0.45;
        ClaudeProgressTrack.Opacity = 0.45;
        ClaudeProProgressTrack.Opacity = 0.45;
        ClaudeProSessionProgressTrack.Opacity = 0.45;
        ClaudeProWeeklyProgressTrack.Opacity = 0.45;
        GeminiProgressTrack.Opacity = 0.45;
        OpenAiDirectProgressTrack.Opacity = 0.45;
        ClaudeDirectProgressTrack.Opacity = 0.45;
        AntigravityProgressTrack.Opacity = 0.45;
        AntigravityGeminiSessionProgressTrack.Opacity = 0.45;
        AntigravityGeminiWeeklyProgressTrack.Opacity = 0.45;
        AntigravityThirdPartySessionProgressTrack.Opacity = 0.45;
        AntigravityThirdPartyWeeklyProgressTrack.Opacity = 0.45;
        OpenRouterProgressTrack.Opacity = 0.45;
        OpenCodeZenProgressTrack.Opacity = 0.45;
        OpenCodeGoProgressTrack.Opacity = 0.45;
        OpenCodeGoRollingProgressTrack.Opacity = 0.45;
        OpenCodeGoWeeklyProgressTrack.Opacity = 0.45;
        OpenCodeGoMonthlyProgressTrack.Opacity = 0.45;
        CursorHeadlineFill.Width = 0;
        OpenAiHeadlineFill.Width = 0;
        ClaudeHeadlineFill.Width = 0;
        GeminiHeadlineFill.Width = 0;
        OpenRouterHeadlineFill.Width = 0;
        OpenCodeHeadlineFill.Width = 0;
        OpenAiDetailText.IsVisible = false;
        ClaudeDetailText.IsVisible = false;
        GeminiDetailText.IsVisible = false;
        OpenAiDirectDetailText.IsVisible = false;
        ClaudeDirectDetailText.IsVisible = false;
        CodexBreakdownSection.IsVisible = false;
        ClaudeProBreakdownSection.IsVisible = false;
        AntigravityBreakdownSection.IsVisible = false;
        OpenCodeGoBreakdownSection.IsVisible = false;
    }

    private void UpdateAllProgressWidths()
    {
        var width = Bounds.Width;
        if (width > 0 && Math.Abs(width - _lastProgressLayoutWidth) > 0.5)
        {
            _lastProgressLayoutWidth = width;
            UpdateProgressWidth(_lastPercentUsed);
            UpdateBreakdownProgressWidths();
            UpdateLimitsProgressWidths();
            UpdateCodexLimitsExpandedState();
            UpdateClaudeProLimitsExpandedState();
            UpdateAntigravityLimitsExpandedState();
            UpdateOpenCodeGoLimitsExpandedState();
            ProviderBarPresenter.UpdateProgressWidth(CursorHeadlineTrack, CursorHeadlineFill, _lastCursorHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenAiHeadlineTrack, OpenAiHeadlineFill, _lastOpenAiHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(ClaudeHeadlineTrack, ClaudeHeadlineFill, _lastClaudeHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(GeminiHeadlineTrack, GeminiHeadlineFill, _lastGeminiHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenRouterHeadlineTrack, OpenRouterHeadlineFill, _lastOpenRouterHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenCodeHeadlineTrack, OpenCodeHeadlineFill, _lastOpenCodeHeadlinePercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenAiProgressTrack, OpenAiProgressFill, _lastOpenAiPercent);
            ProviderBarPresenter.UpdateProgressWidth(ClaudeProgressTrack, ClaudeProgressFill, _lastClaudePercent);
            ProviderBarPresenter.UpdateProgressWidth(GeminiProgressTrack, GeminiProgressFill, _lastGeminiPercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenAiDirectProgressTrack, OpenAiDirectProgressFill, _lastOpenAiDirectPercent);
            ProviderBarPresenter.UpdateProgressWidth(ClaudeDirectProgressTrack, ClaudeDirectProgressFill, _lastClaudeDirectPercent);
            ProviderBarPresenter.UpdateProgressWidth(OpenCodeZenProgressTrack, OpenCodeZenProgressFill, _lastOpenCodeZenPercent);
        }

        UpdateDynamicProgressWidths();
    }

    private void UpdateDynamicProgressWidths()
    {
        UpdateDiskProgressWidths();
        UpdateHardwareProgressWidths();
    }

    private void UpdateLimitsProgressWidths()
    {
        ProviderBarPresenter.UpdateProgressWidth(CodexProgressTrack, CodexProgressFill, _lastCodexPercent);
        ProviderBarPresenter.UpdateProgressWidth(ClaudeProProgressTrack, ClaudeProProgressFill, _lastClaudeProPercent);
        ProviderBarPresenter.UpdateProgressWidth(AntigravityProgressTrack, AntigravityProgressFill, _lastAntigravityPercent);
        ProviderBarPresenter.UpdateProgressWidth(OpenRouterProgressTrack, OpenRouterProgressFill, _lastOpenRouterPercent);
        ProviderBarPresenter.UpdateProgressWidth(OpenCodeGoProgressTrack, OpenCodeGoProgressFill, _lastOpenCodeGoPercent);
        ProviderBarPresenter.UpdateProgressWidth(CodexSessionProgressTrack, CodexSessionProgressFill, _lastCodexSessionPercent);
        ProviderBarPresenter.UpdateProgressWidth(CodexWeeklyProgressTrack, CodexWeeklyProgressFill, _lastCodexWeeklyPercent);
        ProviderBarPresenter.UpdateProgressWidth(ClaudeProSessionProgressTrack, ClaudeProSessionProgressFill, _lastClaudeProSessionPercent);
        ProviderBarPresenter.UpdateProgressWidth(ClaudeProWeeklyProgressTrack, ClaudeProWeeklyProgressFill, _lastClaudeProWeeklyPercent);
        ProviderBarPresenter.UpdateProgressWidth(AntigravityGeminiSessionProgressTrack, AntigravityGeminiSessionProgressFill, _lastAntigravityGeminiSessionPercent);
        ProviderBarPresenter.UpdateProgressWidth(AntigravityGeminiWeeklyProgressTrack, AntigravityGeminiWeeklyProgressFill, _lastAntigravityGeminiWeeklyPercent);
        ProviderBarPresenter.UpdateProgressWidth(AntigravityThirdPartySessionProgressTrack, AntigravityThirdPartySessionProgressFill, _lastAntigravityThirdPartySessionPercent);
        ProviderBarPresenter.UpdateProgressWidth(AntigravityThirdPartyWeeklyProgressTrack, AntigravityThirdPartyWeeklyProgressFill, _lastAntigravityThirdPartyWeeklyPercent);
        ProviderBarPresenter.UpdateProgressWidth(OpenCodeGoRollingProgressTrack, OpenCodeGoRollingProgressFill, _lastOpenCodeGoRollingPercent);
        ProviderBarPresenter.UpdateProgressWidth(OpenCodeGoWeeklyProgressTrack, OpenCodeGoWeeklyProgressFill, _lastOpenCodeGoWeeklyPercent);
        ProviderBarPresenter.UpdateProgressWidth(OpenCodeGoMonthlyProgressTrack, OpenCodeGoMonthlyProgressFill, _lastOpenCodeGoMonthlyPercent);

        CodexSessionProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastCodexSessionPercent);
        CodexWeeklyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastCodexWeeklyPercent);
        ClaudeProSessionProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastClaudeProSessionPercent);
        ClaudeProWeeklyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastClaudeProWeeklyPercent);
        AntigravityGeminiSessionProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastAntigravityGeminiSessionPercent);
        AntigravityGeminiWeeklyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastAntigravityGeminiWeeklyPercent);
        AntigravityThirdPartySessionProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastAntigravityThirdPartySessionPercent);
        AntigravityThirdPartyWeeklyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastAntigravityThirdPartyWeeklyPercent);
        OpenCodeGoRollingProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastOpenCodeGoRollingPercent);
        OpenCodeGoWeeklyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastOpenCodeGoWeeklyPercent);
        OpenCodeGoMonthlyProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastOpenCodeGoMonthlyPercent);
    }

    private void UpdateProgressWidth(double percentUsed)
    {
        var trackWidth = ProgressTrack.Bounds.Width;
        if (trackWidth <= 0)
            return;

        ProgressFill.Width = trackWidth * (percentUsed / 100.0);
    }

    private void UpdateBreakdownProgressWidths()
    {
        var autoTrackWidth = AutoProgressTrack.Bounds.Width;
        if (autoTrackWidth > 0)
            AutoProgressFill.Width = autoTrackWidth * (_lastAutoPercent / 100.0);
        AutoProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastAutoPercent);

        var apiTrackWidth = ApiProgressTrack.Bounds.Width;
        if (apiTrackWidth > 0)
            ApiProgressFill.Width = apiTrackWidth * (_lastApiPercent / 100.0);
        ApiProgressFill.Background = UsageBarBrushes.GetBrushForPercent(_lastApiPercent);
    }

    private void ApplyDiskVolumes(IReadOnlyList<DiskVolumeSnapshot> volumes)
    {
        var oldHeight = Bounds.Height;
        if (!_settings.ShowDiskDrives || volumes.Count == 0)
        {
            DiskSection.IsVisible = false;
            ClearDiskBarRows();
            ScheduleLayoutRefresh(oldHeight);
            return;
        }

        DiskSection.IsVisible = true;
        var volumeNames = volumes.Select(v => v.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        for (var i = _diskBarRows.Count - 1; i >= 0; i--)
        {
            if (!volumeNames.Contains(_diskBarRows[i].Name))
            {
                DiskSection.Children.Remove(_diskBarRows[i].Container);
                _diskBarRows.RemoveAt(i);
            }
        }

        foreach (var volume in volumes)
        {
            var row = _diskBarRows.FirstOrDefault(r =>
                string.Equals(r.Name, volume.Name, StringComparison.OrdinalIgnoreCase));
            if (row is null)
            {
                row = CreateDiskBarRow(volume);
                _diskBarRows.Add(row);
                DiskSection.Children.Add(row.Container);
            }

            ApplyDiskBarRow(row, volume);
        }

        ReorderDiskBarRows(volumes);
        UpdateDiskProgressWidths();
        ScheduleLayoutRefresh(oldHeight);
    }

    private void ReorderDiskBarRows(IReadOnlyList<DiskVolumeSnapshot> volumes)
    {
        foreach (var row in _diskBarRows)
            DiskSection.Children.Remove(row.Container);

        foreach (var volume in volumes)
        {
            var row = _diskBarRows.First(r =>
                string.Equals(r.Name, volume.Name, StringComparison.OrdinalIgnoreCase));
            DiskSection.Children.Add(row.Container);
        }
    }

    private void ApplyDiskBarRow(DiskBarRow row, DiskVolumeSnapshot volume)
    {
        if (row.Container.Children[0] is Grid barGrid &&
            barGrid.Children[0] is TextBlock label)
        {
            label.Text = volume.DisplayLabel;
        }

        var lastPercent = volume.PercentUsed;
        ProviderBarPresenter.ApplyUsageBar(
            row.Track,
            row.Fill,
            ref lastPercent,
            volume.PercentUsed,
            true,
            null,
            volume.DetailLabel,
            _settings.ShowDiskDetails,
            row.Detail);
        row.LastPercent = lastPercent;
    }

    private DiskBarRow CreateDiskBarRow(DiskVolumeSnapshot volume)
    {
        var label = new TextBlock
        {
            Text = volume.DisplayLabel,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
            FontFamily = new FontFamily("Segoe UI Semibold, Segoe UI, sans-serif"),
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xB0, 0xB0, 0xB0)),
            MaxWidth = 52,
            TextTrimming = TextTrimming.CharacterEllipsis
        };

        var fill = new Border
        {
            CornerRadius = new CornerRadius(3),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Width = 0,
            Background = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50))
        };

        var track = new Grid
        {
            Height = 6,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Children =
            {
                new Border { CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF)) },
                fill
            }
        };

        var barGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            Children = { label, track }
        };
        Grid.SetColumn(label, 0);
        Grid.SetColumn(track, 1);

        var detail = new TextBlock
        {
            Margin = new Thickness(0, 2, 0, 4),
            FontSize = 9,
            LineHeight = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0x77, 0x77, 0x77)),
            IsVisible = _settings.ShowDiskDetails
        };

        var container = new StackPanel
        {
            Spacing = 0,
            Children = { barGrid, detail }
        };

        return new DiskBarRow
        {
            Name = volume.Name,
            Container = container,
            Track = track,
            Fill = fill,
            Detail = detail
        };
    }

    private void ClearDiskBarRows()
    {
        DiskSection.Children.Clear();
        _diskBarRows.Clear();
    }

    private void UpdateDiskProgressWidths()
    {
        foreach (var row in _diskBarRows)
            ProviderBarPresenter.UpdateProgressWidth(row.Track, row.Fill, row.LastPercent);
    }

    private void RefreshWindowHeight()
    {
        if (!SizeToContent.HasFlag(SizeToContent.Height))
            return;

        Dispatcher.UIThread.Post(() =>
        {
            SizeToContent = SizeToContent.Manual;
            SizeToContent = SizeToContent.Height;
        }, DispatcherPriority.Loaded);
    }

    private void UpdateLastRefreshedLabel()
    {
        var label = _widgetViewModel.LastRefreshedLabel;
        LastRefreshedText.Text = label;
        LastRefreshedText.IsVisible = !string.IsNullOrWhiteSpace(label);
    }

    private void ApplyDegradedTooltips()
    {
        ToolTip.SetTip(OpenAiProviderHeader, _widgetViewModel.OpenAi.DegradedMessage);
        ToolTip.SetTip(GeminiProviderHeader, _widgetViewModel.Gemini.DegradedMessage);
        ToolTip.SetTip(OpenRouterProviderHeader, _widgetViewModel.OpenRouter.DegradedMessage);
        ToolTip.SetTip(OpenCodeProviderHeader, _widgetViewModel.OpenCode.DegradedMessage);
    }

    private void SaveSettings()
    {
        SettingsPanelControl.CommitToSettings(_settings);
        if (_settings.IsPositionPinned)
        {
            _settings.Left = Position.X;
            _settings.Top = Position.Y;
        }
        _settings.IsCodexLimitsExpanded = _isCodexLimitsExpanded;
        _settings.IsClaudeProLimitsExpanded = _isClaudeProLimitsExpanded;
        _settings.IsAntigravityLimitsExpanded = _isAntigravityLimitsExpanded;
        _settings.IsOpenCodeGoLimitsExpanded = _isOpenCodeGoLimitsExpanded;
        _settings.IsSettingsExpanded = _isSettingsExpanded;
        _settings.IsCursorProviderExpanded = _isCursorProviderExpanded;
        _settings.IsOpenAiProviderExpanded = _isOpenAiProviderExpanded;
        _settings.IsClaudeProviderExpanded = _isClaudeProviderExpanded;
        _settings.IsGeminiProviderExpanded = _isGeminiProviderExpanded;
        _settings.IsOpenRouterProviderExpanded = _isOpenRouterProviderExpanded;
        _settings.IsOpenCodeProviderExpanded = _isOpenCodeProviderExpanded;
        _settings.SettingsExpandedProvider = _settingsViewModel.ExpandedProvider;
        SettingsStore.Save(_settings);
    }

    protected override void OnClosed(EventArgs e)
    {
        _pollTimer.Stop();
        _hardwareTimer.Stop();
        _debouncedPositionSave.Dispose();
        _refreshService.Dispose();
        _hardwareMetricsProvider.Dispose();
        _usageClient.Dispose();
        _directBilling.Dispose();
        _openAiBilling.Dispose();
        _codexBilling.Dispose();
        _anthropicBilling.Dispose();
        _claudeProBilling.Dispose();
        _antigravityBilling.Dispose();
        _openRouterBilling.Dispose();
        _openCodeBilling.Dispose();
        base.OnClosed(e);
    }
}

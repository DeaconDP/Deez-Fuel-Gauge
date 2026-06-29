using System.Globalization;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

public sealed class SettingsPanelViewModel : ViewModelBase
{
    private readonly ProviderEasySetupService _easySetup;
    private readonly OpenAiBillingClient _openAiBilling;
    private readonly CodexUsageClient _codexBilling;
    private readonly AnthropicBillingClient _anthropicBilling;
    private readonly ClaudeProUsageClient _claudeProBilling;
    private readonly AntigravityUsageClient _antigravityBilling;
    private readonly OpenRouterUsageClient _openRouterBilling;
    private readonly OpenCodeUsageClient _openCodeBilling;
    private readonly Func<CursorTokens> _cursorTokenReader;
    private readonly Func<AntigravityOAuthTokens> _antigravityTokenReader;
    private ISettingsPanelHost? _host;
    private bool _suppressChangeNotifications;

    private string? _openAiCredentialId;
    private string? _openAiProSessionCredentialId;
    private string? _claudeProSessionCredentialId;
    private string? _claudeCredentialId;

    private bool _showCursor = true;
    private bool _showCursorDetails = true;
    private bool _showBreakdown = true;
    private string _cursorStatus = "";

    private bool _showOpenAi = true;
    private bool _showOpenAiDetails = true;
    private bool _showOpenAiDirect;
    private bool _showOpenAiDirectDetails = true;
    private bool _showCodexLimits = true;
    private bool _showCodexDetails = true;
    private bool _showCodexBreakdown = true;
    private string _openAiOrgId = "";
    private string _openAiBudget = "";
    private string _openAiApiKeyWatermark = "Admin API key";
    private string _openAiSessionCookieWatermark = "ChatGPT session cookie (optional fallback)";
    private string _openAiStatus = "";
    private string _openAiCursorPlanStatus = "";
    private string _codexStatus = "";

    private bool _showClaude = true;
    private bool _showClaudeDetails = true;
    private bool _showClaudePro = true;
    private bool _showClaudeProDetails = true;
    private bool _showClaudeProBreakdown = true;
    private bool _showClaudeApiConsole;
    private bool _showClaudeApiConsoleDetails = true;
    private string _claudeBudget = "";
    private string _claudeApiKeyWatermark = "Admin API key (sk-ant-admin...)";
    private string _claudeProStatus = "";
    private string _claudeApiConsoleStatus = "";
    private string _claudeCursorPlanStatus = "";

    private bool _showGemini = true;
    private bool _showGeminiDetails = true;
    private bool _showAntigravityLimits = true;
    private bool _showAntigravityDetails = true;
    private bool _showAntigravityBreakdown = true;
    private string _antigravityStatus = "";
    private string _geminiCursorPlanStatus = "";

    private bool _showOpenRouterLimits = true;
    private bool _showOpenRouterDetails = true;
    private string _openRouterApiKeyWatermark = "API key (sk-or-...)";
    private string _openRouterStatus = "";
    private string? _openRouterCredentialId;

    private bool _showOpenCodeZen = true;
    private bool _showOpenCodeGo = true;
    private bool _showOpenCodeZenDetails = true;
    private bool _showOpenCodeGoDetails = true;
    private bool _showOpenCodeGoBreakdown = true;
    private string _openCodeWorkspaceId = "";
    private string _openCodeSessionWatermark = "opencode.ai auth cookie";
    private string _openCodeStatus = "";
    private string? _openCodeProSessionCredentialId;

    private bool _showDiskDrives = true;
    private bool _showDiskDetails = true;
    private int _refreshIntervalMinutes = 5;
    private bool _launchAtLogin;

    private bool _quotaAlertsEnabled = true;
    private int _quotaAlertDaysBeforeEnd = 7;
    private int _quotaAlertMaxPercentUsed = 75;
    private bool _quotaAlertCursorPlan = true;
    private bool _quotaAlertOpenAiCursor;
    private bool _quotaAlertOpenAiPlatform = true;
    private bool _quotaAlertClaudeCursor;
    private bool _quotaAlertClaudeApi = true;
    private bool _quotaAlertGeminiCursor;
    private bool _quotaAlertOpenRouterKeyLimit = true;
    private bool _quotaAlertOpenCodeZenMonthly = true;
    private bool _quotaAlertOpenCodeGoMonthly = true;

    private SettingsExpandedProvider _expandedProvider = SettingsExpandedProvider.None;

    private ProviderConnectionState _cursorConnectionState = ProviderConnectionState.Off;
    private ProviderConnectionState _openAiHeaderState = ProviderConnectionState.Off;
    private ProviderConnectionState _claudeHeaderState = ProviderConnectionState.Off;
    private ProviderConnectionState _geminiHeaderState = ProviderConnectionState.Off;
    private ProviderConnectionState _openRouterHeaderState = ProviderConnectionState.Off;
    private ProviderConnectionState _openCodeHeaderState = ProviderConnectionState.Off;

    public SettingsPanelViewModel(
        ProviderEasySetupService easySetup,
        OpenAiBillingClient openAiBilling,
        CodexUsageClient codexBilling,
        AnthropicBillingClient anthropicBilling,
        ClaudeProUsageClient claudeProBilling,
        AntigravityUsageClient antigravityBilling,
        OpenRouterUsageClient openRouterBilling,
        OpenCodeUsageClient openCodeBilling,
        Func<CursorTokens>? cursorTokenReader = null,
        Func<AntigravityOAuthTokens>? antigravityTokenReader = null)
    {
        _easySetup = easySetup;
        _openAiBilling = openAiBilling;
        _codexBilling = codexBilling;
        _anthropicBilling = anthropicBilling;
        _claudeProBilling = claudeProBilling;
        _antigravityBilling = antigravityBilling;
        _openRouterBilling = openRouterBilling;
        _openCodeBilling = openCodeBilling;
        _cursorTokenReader = cursorTokenReader ?? CursorTokenReader.Read;
        _antigravityTokenReader = antigravityTokenReader ?? AntigravityTokenReader.Read;
    }

    public void AttachHost(ISettingsPanelHost host) => _host = host;

    public bool ShowCursor
    {
        get => _showCursor;
        set => SetToggle(ref _showCursor, value);
    }

    public bool ShowCursorDetails
    {
        get => _showCursorDetails;
        set => SetToggle(ref _showCursorDetails, value);
    }

    public bool ShowBreakdown
    {
        get => _showBreakdown;
        set => SetToggle(ref _showBreakdown, value);
    }

    public string CursorStatus
    {
        get => _cursorStatus;
        set => SetProperty(ref _cursorStatus, value);
    }

    public bool ShowOpenAi
    {
        get => _showOpenAi;
        set => SetToggle(ref _showOpenAi, value);
    }

    public bool ShowOpenAiDetails
    {
        get => _showOpenAiDetails;
        set => SetToggle(ref _showOpenAiDetails, value);
    }

    public bool ShowOpenAiDirect
    {
        get => _showOpenAiDirect;
        set
        {
            if (SetToggle(ref _showOpenAiDirect, value))
            {
                OnPropertyChanged(nameof(ShowOpenAiDirectCredentials));
                UpdateConnectionStates();
            }
        }
    }

    public bool ShowOpenAiDirectDetails
    {
        get => _showOpenAiDirectDetails;
        set => SetToggle(ref _showOpenAiDirectDetails, value);
    }

    public bool ShowCodexLimits
    {
        get => _showCodexLimits;
        set
        {
            if (SetToggle(ref _showCodexLimits, value))
            {
                OnPropertyChanged(nameof(ShowCodexCredentials));
                UpdateConnectionStates();
            }
        }
    }

    public bool ShowCodexDetails
    {
        get => _showCodexDetails;
        set => SetToggle(ref _showCodexDetails, value);
    }

    public bool ShowCodexBreakdown
    {
        get => _showCodexBreakdown;
        set => SetToggle(ref _showCodexBreakdown, value);
    }

    public string OpenAiOrgId
    {
        get => _openAiOrgId;
        set
        {
            if (SetProperty(ref _openAiOrgId, value))
                NotifyFieldChanged();
        }
    }

    public string OpenAiBudget
    {
        get => _openAiBudget;
        set
        {
            if (SetProperty(ref _openAiBudget, value))
                NotifyFieldChanged();
        }
    }

    public string OpenAiApiKeyWatermark
    {
        get => _openAiApiKeyWatermark;
        private set
        {
            if (SetProperty(ref _openAiApiKeyWatermark, value))
                OnPropertyChanged(nameof(HasOpenAiApiKeySaved));
        }
    }

    public bool HasOpenAiApiKeySaved => !string.IsNullOrWhiteSpace(_openAiCredentialId);

    public bool ShowOpenAiDirectCredentials => ShowOpenAiDirect && !HasOpenAiApiKeySaved;

    public bool HasCodexAutoAuth => _codexBilling.HasLocalAuthFile();

    public bool ShowCodexCredentials =>
        ShowCodexLimits && !HasCodexAutoAuth && !HasOpenAiSessionCookieSaved;

    public string OpenAiSessionCookieWatermark
    {
        get => _openAiSessionCookieWatermark;
        private set
        {
            if (SetProperty(ref _openAiSessionCookieWatermark, value))
                OnPropertyChanged(nameof(HasOpenAiSessionCookieSaved));
        }
    }

    public bool HasOpenAiSessionCookieSaved => !string.IsNullOrWhiteSpace(_openAiProSessionCredentialId);

    public string OpenAiStatus
    {
        get => _openAiStatus;
        set => SetProperty(ref _openAiStatus, value);
    }

    public string OpenAiCursorPlanStatus
    {
        get => _openAiCursorPlanStatus;
        set => SetProperty(ref _openAiCursorPlanStatus, value);
    }

    public string CodexStatus
    {
        get => _codexStatus;
        set => SetProperty(ref _codexStatus, value);
    }

    public bool ShowClaude
    {
        get => _showClaude;
        set => SetToggle(ref _showClaude, value);
    }

    public bool ShowClaudeDetails
    {
        get => _showClaudeDetails;
        set => SetToggle(ref _showClaudeDetails, value);
    }

    public bool ShowClaudePro
    {
        get => _showClaudePro;
        set
        {
            if (SetToggle(ref _showClaudePro, value))
                UpdateConnectionStates();
        }
    }

    public bool ShowClaudeProDetails
    {
        get => _showClaudeProDetails;
        set => SetToggle(ref _showClaudeProDetails, value);
    }

    public bool ShowClaudeProBreakdown
    {
        get => _showClaudeProBreakdown;
        set => SetToggle(ref _showClaudeProBreakdown, value);
    }

    public bool ShowClaudeApiConsole
    {
        get => _showClaudeApiConsole;
        set
        {
            if (SetToggle(ref _showClaudeApiConsole, value))
            {
                OnPropertyChanged(nameof(ShowClaudeApiConsoleCredentials));
                UpdateConnectionStates();
            }
        }
    }

    public bool ShowClaudeApiConsoleCredentials => ShowClaudeApiConsole && !HasClaudeApiKeySaved;

    public bool ShowClaudeApiConsoleDetails
    {
        get => _showClaudeApiConsoleDetails;
        set => SetToggle(ref _showClaudeApiConsoleDetails, value);
    }

    public string ClaudeBudget
    {
        get => _claudeBudget;
        set
        {
            if (SetProperty(ref _claudeBudget, value))
                NotifyFieldChanged();
        }
    }

    public bool HasClaudeProAuth =>
        HasClaudeSessionCookieSaved
        || (ClaudeCodeTokenReader.Read() is { IsExpired: false });

    public bool HasClaudeSessionCookieSaved => !string.IsNullOrWhiteSpace(_claudeProSessionCredentialId);

    public string ClaudeApiKeyWatermark
    {
        get => _claudeApiKeyWatermark;
        private set
        {
            if (SetProperty(ref _claudeApiKeyWatermark, value))
                OnPropertyChanged(nameof(HasClaudeApiKeySaved));
        }
    }

    public bool HasClaudeApiKeySaved => !string.IsNullOrWhiteSpace(_claudeCredentialId);

    public string ClaudeProStatus
    {
        get => _claudeProStatus;
        set => SetProperty(ref _claudeProStatus, value);
    }

    public string ClaudeApiConsoleStatus
    {
        get => _claudeApiConsoleStatus;
        set => SetProperty(ref _claudeApiConsoleStatus, value);
    }

    public string ClaudeCursorPlanStatus
    {
        get => _claudeCursorPlanStatus;
        private set => SetProperty(ref _claudeCursorPlanStatus, value);
    }

    public bool ShowGemini
    {
        get => _showGemini;
        set => SetToggle(ref _showGemini, value);
    }

    public bool ShowGeminiDetails
    {
        get => _showGeminiDetails;
        set => SetToggle(ref _showGeminiDetails, value);
    }

    public bool ShowAntigravityLimits
    {
        get => _showAntigravityLimits;
        set => SetToggle(ref _showAntigravityLimits, value);
    }

    public bool ShowAntigravityDetails
    {
        get => _showAntigravityDetails;
        set => SetToggle(ref _showAntigravityDetails, value);
    }

    public bool ShowAntigravityBreakdown
    {
        get => _showAntigravityBreakdown;
        set => SetToggle(ref _showAntigravityBreakdown, value);
    }

    public string AntigravityStatus
    {
        get => _antigravityStatus;
        set => SetProperty(ref _antigravityStatus, value);
    }

    public string GeminiCursorPlanStatus
    {
        get => _geminiCursorPlanStatus;
        set => SetProperty(ref _geminiCursorPlanStatus, value);
    }

    public bool ShowOpenRouterLimits
    {
        get => _showOpenRouterLimits;
        set
        {
            if (SetToggle(ref _showOpenRouterLimits, value))
            {
                OnPropertyChanged(nameof(ShowOpenRouterCredentials));
                UpdateConnectionStates();
            }
        }
    }

    public bool ShowOpenRouterDetails
    {
        get => _showOpenRouterDetails;
        set => SetToggle(ref _showOpenRouterDetails, value);
    }

    public string OpenRouterApiKeyWatermark
    {
        get => _openRouterApiKeyWatermark;
        private set => SetProperty(ref _openRouterApiKeyWatermark, value);
    }

    public string OpenRouterStatus
    {
        get => _openRouterStatus;
        set => SetProperty(ref _openRouterStatus, value);
    }

    public bool HasOpenRouterApiKeySaved => !string.IsNullOrWhiteSpace(_openRouterCredentialId);

    public bool ShowOpenRouterCredentials => ShowOpenRouterLimits && !HasOpenRouterApiKeySaved;

    public bool ShowOpenCodeZen
    {
        get => _showOpenCodeZen;
        set => SetToggle(ref _showOpenCodeZen, value);
    }

    public bool ShowOpenCodeGo
    {
        get => _showOpenCodeGo;
        set => SetToggle(ref _showOpenCodeGo, value);
    }

    public bool ShowOpenCodeZenDetails
    {
        get => _showOpenCodeZenDetails;
        set => SetToggle(ref _showOpenCodeZenDetails, value);
    }

    public bool ShowOpenCodeGoDetails
    {
        get => _showOpenCodeGoDetails;
        set => SetToggle(ref _showOpenCodeGoDetails, value);
    }

    public bool ShowOpenCodeGoBreakdown
    {
        get => _showOpenCodeGoBreakdown;
        set => SetToggle(ref _showOpenCodeGoBreakdown, value);
    }

    public string OpenCodeWorkspaceId
    {
        get => _openCodeWorkspaceId;
        set => SetProperty(ref _openCodeWorkspaceId, value);
    }

    public string OpenCodeSessionWatermark
    {
        get => _openCodeSessionWatermark;
        private set => SetProperty(ref _openCodeSessionWatermark, value);
    }

    public string OpenCodeStatus
    {
        get => _openCodeStatus;
        set => SetProperty(ref _openCodeStatus, value);
    }

    public bool HasOpenCodeSessionSaved => !string.IsNullOrWhiteSpace(_openCodeProSessionCredentialId);

    public bool ShowDiskDrives
    {
        get => _showDiskDrives;
        set
        {
            if (SetToggle(ref _showDiskDrives, value))
                OnPropertyChanged(nameof(DiskHeaderColor));
        }
    }

    public bool ShowDiskDetails
    {
        get => _showDiskDetails;
        set => SetToggle(ref _showDiskDetails, value);
    }

    public int RefreshIntervalMinutes
    {
        get => _refreshIntervalMinutes;
        set
        {
            var clamped = Math.Clamp(value, 1, 120);
            if (SetProperty(ref _refreshIntervalMinutes, clamped))
            {
                OnPropertyChanged(nameof(RefreshIntervalText));
                NotifyFieldChanged();
            }
        }
    }

    public string RefreshIntervalText
    {
        get => _refreshIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutes))
                RefreshIntervalMinutes = minutes;
        }
    }

    public bool LaunchAtLogin
    {
        get => _launchAtLogin;
        set
        {
            if (SetToggle(ref _launchAtLogin, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertsEnabled
    {
        get => _quotaAlertsEnabled;
        set
        {
            if (SetToggle(ref _quotaAlertsEnabled, value))
                NotifyFieldChanged();
        }
    }

    public int QuotaAlertDaysBeforeEnd
    {
        get => _quotaAlertDaysBeforeEnd;
        set
        {
            var clamped = Math.Clamp(value, 1, 31);
            if (SetProperty(ref _quotaAlertDaysBeforeEnd, clamped))
            {
                OnPropertyChanged(nameof(QuotaAlertDaysBeforeEndText));
                NotifyFieldChanged();
            }
        }
    }

    public string QuotaAlertDaysBeforeEndText
    {
        get => _quotaAlertDaysBeforeEnd.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
                QuotaAlertDaysBeforeEnd = days;
        }
    }

    public int QuotaAlertMaxPercentUsed
    {
        get => _quotaAlertMaxPercentUsed;
        set
        {
            var clamped = Math.Clamp(value, 1, 99);
            if (SetProperty(ref _quotaAlertMaxPercentUsed, clamped))
            {
                OnPropertyChanged(nameof(QuotaAlertMaxPercentUsedText));
                NotifyFieldChanged();
            }
        }
    }

    public string QuotaAlertMaxPercentUsedText
    {
        get => _quotaAlertMaxPercentUsed.ToString(CultureInfo.InvariantCulture);
        set
        {
            if (int.TryParse(value?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent))
                QuotaAlertMaxPercentUsed = percent;
        }
    }

    public bool QuotaAlertCursorPlan
    {
        get => _quotaAlertCursorPlan;
        set
        {
            if (SetToggle(ref _quotaAlertCursorPlan, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertOpenAiCursor
    {
        get => _quotaAlertOpenAiCursor;
        set
        {
            if (SetToggle(ref _quotaAlertOpenAiCursor, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertOpenAiPlatform
    {
        get => _quotaAlertOpenAiPlatform;
        set
        {
            if (SetToggle(ref _quotaAlertOpenAiPlatform, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertClaudeCursor
    {
        get => _quotaAlertClaudeCursor;
        set
        {
            if (SetToggle(ref _quotaAlertClaudeCursor, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertClaudeApi
    {
        get => _quotaAlertClaudeApi;
        set
        {
            if (SetToggle(ref _quotaAlertClaudeApi, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertGeminiCursor
    {
        get => _quotaAlertGeminiCursor;
        set
        {
            if (SetToggle(ref _quotaAlertGeminiCursor, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertOpenRouterKeyLimit
    {
        get => _quotaAlertOpenRouterKeyLimit;
        set
        {
            if (SetToggle(ref _quotaAlertOpenRouterKeyLimit, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertOpenCodeZenMonthly
    {
        get => _quotaAlertOpenCodeZenMonthly;
        set
        {
            if (SetToggle(ref _quotaAlertOpenCodeZenMonthly, value))
                NotifyFieldChanged();
        }
    }

    public bool QuotaAlertOpenCodeGoMonthly
    {
        get => _quotaAlertOpenCodeGoMonthly;
        set
        {
            if (SetToggle(ref _quotaAlertOpenCodeGoMonthly, value))
                NotifyFieldChanged();
        }
    }

    public SettingsExpandedProvider ExpandedProvider
    {
        get => _expandedProvider;
        set
        {
            if (!SetProperty(ref _expandedProvider, value))
                return;

            NotifyAccordionPropertiesChanged();
            _host?.OnSettingsLayoutChanged();
        }
    }

    public bool IsCursorExpanded => ExpandedProvider == SettingsExpandedProvider.Cursor;
    public bool IsOpenAiExpanded => ExpandedProvider == SettingsExpandedProvider.OpenAi;
    public bool IsClaudeExpanded => ExpandedProvider == SettingsExpandedProvider.Claude;
    public bool IsGeminiExpanded => ExpandedProvider == SettingsExpandedProvider.Gemini;
    public bool IsOpenRouterExpanded => ExpandedProvider == SettingsExpandedProvider.OpenRouter;
    public bool IsOpenCodeExpanded => ExpandedProvider == SettingsExpandedProvider.OpenCode;
    public bool IsDiskExpanded => ExpandedProvider == SettingsExpandedProvider.Disk;
    public bool IsWidgetExpanded => ExpandedProvider == SettingsExpandedProvider.Widget;

    public string CursorChevron => IsCursorExpanded ? "▴" : "▾";
    public string OpenAiChevron => IsOpenAiExpanded ? "▴" : "▾";
    public string ClaudeChevron => IsClaudeExpanded ? "▴" : "▾";
    public string GeminiChevron => IsGeminiExpanded ? "▴" : "▾";
    public string OpenRouterChevron => IsOpenRouterExpanded ? "▴" : "▾";
    public string OpenCodeChevron => IsOpenCodeExpanded ? "▴" : "▾";
    public string DiskChevron => IsDiskExpanded ? "▴" : "▾";
    public string WidgetChevron => IsWidgetExpanded ? "▴" : "▾";

    public string CursorHeaderColor => ProviderConnectionStateHelper.ToColor(_cursorConnectionState);
    public string OpenAiHeaderColor => ProviderConnectionStateHelper.ToColor(_openAiHeaderState);
    public string ClaudeHeaderColor => ProviderConnectionStateHelper.ToColor(_claudeHeaderState);
    public string GeminiHeaderColor => ProviderConnectionStateHelper.ToColor(_geminiHeaderState);
    public string OpenRouterHeaderColor => ProviderConnectionStateHelper.ToColor(_openRouterHeaderState);
    public string OpenCodeHeaderColor => ProviderConnectionStateHelper.ToColor(_openCodeHeaderState);
    public string DiskHeaderColor => ProviderConnectionStateHelper.ToColor(
        ShowDiskDrives ? ProviderConnectionState.Connected : ProviderConnectionState.Off);
    public string WidgetHeaderColor => ProviderConnectionStateHelper.ToColor(ProviderConnectionState.Connected);

    public void ToggleExpandedProvider(SettingsExpandedProvider provider) =>
        ExpandedProvider = ExpandedProvider == provider ? SettingsExpandedProvider.None : provider;

    public void Load(WidgetSettings settings)
    {
        _suppressChangeNotifications = true;
        try
        {
            _openAiCredentialId = settings.OpenAi.CredentialId;
            _openAiProSessionCredentialId = settings.OpenAi.ProSessionCredentialId;
            _claudeProSessionCredentialId = settings.Claude.ProSessionCredentialId;
            _claudeCredentialId = settings.Claude.CredentialId;

            ShowCursor = settings.Cursor.ShowCursorSource;
            ShowCursorDetails = settings.Cursor.ShowDetails;
            ShowBreakdown = settings.ShowBreakdown;

            ShowOpenAi = settings.OpenAi.ShowCursorSource;
            ShowOpenAiDetails = settings.OpenAi.ShowDetails;
            ShowOpenAiDirect = settings.OpenAi.ShowDirectSource;
            ShowOpenAiDirectDetails = settings.OpenAi.EffectiveShowDirectDetails;
            ShowCodexLimits = settings.OpenAi.ShowProLimits;
            ShowCodexDetails = settings.OpenAi.EffectiveShowProDetails;
            ShowCodexBreakdown = settings.OpenAi.ShowProBreakdown;
            OpenAiOrgId = settings.OpenAi.OrganizationId ?? "";
            OpenAiBudget = FormatBudget(settings.OpenAi.MonthlyBudgetUsd);
            OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? "";
            CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? "";

            ShowClaude = settings.Claude.ShowCursorSource;
            ShowClaudeDetails = settings.Claude.ShowDetails;
            ShowClaudePro = settings.Claude.ShowProLimits;
            ShowClaudeProDetails = settings.Claude.EffectiveShowProDetails;
            ShowClaudeProBreakdown = settings.Claude.ShowProBreakdown;
            ShowClaudeApiConsole = settings.Claude.ShowApiConsoleBilling;
            ShowClaudeApiConsoleDetails = settings.Claude.EffectiveShowDirectDetails;
            ClaudeBudget = FormatBudget(settings.Claude.MonthlyBudgetUsd);
            ClaudeProStatus = settings.Claude.ProLastConnectionStatus ?? "";
            ClaudeApiConsoleStatus = settings.Claude.LastConnectionStatus ?? "";

            ShowGemini = settings.Gemini.ShowCursorSource;
            ShowGeminiDetails = settings.Gemini.ShowDetails;
            ShowAntigravityLimits = settings.Gemini.ShowProLimits;
            ShowAntigravityDetails = settings.Gemini.EffectiveShowProDetails;
            ShowAntigravityBreakdown = settings.Gemini.ShowProBreakdown;
            AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? "";

            _openRouterCredentialId = settings.OpenRouter.CredentialId;
            ShowOpenRouterLimits = settings.OpenRouter.ShowProLimits;
            ShowOpenRouterDetails = settings.OpenRouter.ShowDetails;
            OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? "";

            _openCodeProSessionCredentialId = settings.OpenCode.ProSessionCredentialId;
            ShowOpenCodeZen = settings.OpenCode.ShowDirectSource;
            ShowOpenCodeGo = settings.OpenCode.ShowProLimits;
            ShowOpenCodeZenDetails = settings.OpenCode.ShowDetails;
            ShowOpenCodeGoDetails = settings.OpenCode.EffectiveShowProDetails;
            ShowOpenCodeGoBreakdown = settings.OpenCode.ShowProBreakdown;
            OpenCodeWorkspaceId = settings.OpenCode.WorkspaceId ?? "";
            OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? "";

            ShowDiskDrives = settings.ShowDiskDrives;
            ShowDiskDetails = settings.ShowDiskDetails;
            RefreshIntervalMinutes = settings.RefreshIntervalMinutes > 0 ? settings.RefreshIntervalMinutes : 5;
            LaunchAtLogin = settings.LaunchAtLogin;

            var quotaAlerts = settings.QuotaAlerts;
            QuotaAlertsEnabled = quotaAlerts.Enabled;
            QuotaAlertDaysBeforeEnd = quotaAlerts.DaysBeforePeriodEnd > 0 ? quotaAlerts.DaysBeforePeriodEnd : 7;
            QuotaAlertMaxPercentUsed = quotaAlerts.MaxPercentUsed is > 0 and < 100
                ? quotaAlerts.MaxPercentUsed
                : 75;
            QuotaAlertCursorPlan = quotaAlerts.CursorPlan;
            QuotaAlertOpenAiCursor = quotaAlerts.OpenAiCursor;
            QuotaAlertOpenAiPlatform = quotaAlerts.OpenAiPlatform;
            QuotaAlertClaudeCursor = quotaAlerts.ClaudeCursor;
            QuotaAlertClaudeApi = quotaAlerts.ClaudeApi;
            QuotaAlertGeminiCursor = quotaAlerts.GeminiCursor;
            QuotaAlertOpenRouterKeyLimit = quotaAlerts.OpenRouterKeyLimit;
            QuotaAlertOpenCodeZenMonthly = quotaAlerts.OpenCodeZenMonthly;
            QuotaAlertOpenCodeGoMonthly = quotaAlerts.OpenCodeGoMonthly;

            _expandedProvider = settings.SettingsExpandedProvider;
            NotifyAccordionPropertiesChanged();

            UpdateCredentialWatermarks();
            UpdateCursorConnectionStatus();
            UpdateConnectionStates();
        }
        finally
        {
            _suppressChangeNotifications = false;
        }
    }

    public void Commit(WidgetSettings settings)
    {
        settings.Cursor.ShowCursorSource = ShowCursor;
        settings.Cursor.ShowDetails = ShowCursorDetails;
        settings.ShowBreakdown = ShowBreakdown;

        settings.OpenAi.ShowCursorSource = ShowOpenAi;
        settings.OpenAi.ShowDetails = ShowOpenAiDetails;
        settings.OpenAi.ShowDirectSource = ShowOpenAiDirect;
        settings.OpenAi.ShowDirectDetails = ShowOpenAiDirectDetails;
        settings.OpenAi.ShowProLimits = ShowCodexLimits;
        settings.OpenAi.ShowProDetails = ShowCodexDetails;
        settings.OpenAi.ShowProBreakdown = ShowCodexBreakdown;
        settings.OpenAi.OrganizationId = NullIfEmpty(OpenAiOrgId);
        settings.OpenAi.MonthlyBudgetUsd = ParseBudget(OpenAiBudget);
        settings.OpenAi.CredentialId = _openAiCredentialId;
        settings.OpenAi.ProSessionCredentialId = _openAiProSessionCredentialId;
        settings.OpenAi.LastConnectionStatus = NullIfEmpty(OpenAiStatus);
        settings.OpenAi.ProLastConnectionStatus = NullIfEmpty(CodexStatus);

        settings.Claude.ShowCursorSource = ShowClaude;
        settings.Claude.ShowDetails = ShowClaudeDetails;
        settings.Claude.ShowProLimits = ShowClaudePro;
        settings.Claude.ShowProDetails = ShowClaudeProDetails;
        settings.Claude.ShowProBreakdown = ShowClaudeProBreakdown;
        settings.Claude.ShowApiConsoleBilling = ShowClaudeApiConsole;
        settings.Claude.ShowDirectDetails = ShowClaudeApiConsoleDetails;
        settings.Claude.MonthlyBudgetUsd = ParseBudget(ClaudeBudget);
        settings.Claude.ProSessionCredentialId = _claudeProSessionCredentialId;
        settings.Claude.CredentialId = _claudeCredentialId;
        settings.Claude.ProLastConnectionStatus = NullIfEmpty(ClaudeProStatus);
        settings.Claude.LastConnectionStatus = NullIfEmpty(ClaudeApiConsoleStatus);

        settings.Gemini.ShowCursorSource = ShowGemini;
        settings.Gemini.ShowDetails = ShowGeminiDetails;
        settings.Gemini.ShowProLimits = ShowAntigravityLimits;
        settings.Gemini.ShowProDetails = ShowAntigravityDetails;
        settings.Gemini.ShowProBreakdown = ShowAntigravityBreakdown;
        settings.Gemini.ProLastConnectionStatus = NullIfEmpty(AntigravityStatus);

        settings.OpenRouter.ShowProLimits = ShowOpenRouterLimits;
        settings.OpenRouter.ShowDetails = ShowOpenRouterDetails;
        settings.OpenRouter.CredentialId = _openRouterCredentialId;
        settings.OpenRouter.LastConnectionStatus = NullIfEmpty(OpenRouterStatus);

        settings.OpenCode.ShowDirectSource = ShowOpenCodeZen;
        settings.OpenCode.ShowProLimits = ShowOpenCodeGo;
        settings.OpenCode.ShowDetails = ShowOpenCodeZenDetails;
        settings.OpenCode.ShowProDetails = ShowOpenCodeGoDetails;
        settings.OpenCode.ShowProBreakdown = ShowOpenCodeGoBreakdown;
        settings.OpenCode.WorkspaceId = NullIfEmpty(OpenCodeWorkspaceId);
        settings.OpenCode.ProSessionCredentialId = _openCodeProSessionCredentialId;
        settings.OpenCode.ProLastConnectionStatus = NullIfEmpty(OpenCodeStatus);

        settings.ShowDiskDrives = ShowDiskDrives;
        settings.ShowDiskDetails = ShowDiskDetails;
        settings.RefreshIntervalMinutes = RefreshIntervalMinutes;
        settings.LaunchAtLogin = LaunchAtLogin;
        settings.QuotaAlerts = new QuotaAlertSettings
        {
            Enabled = QuotaAlertsEnabled,
            DaysBeforePeriodEnd = QuotaAlertDaysBeforeEnd,
            MaxPercentUsed = QuotaAlertMaxPercentUsed,
            CursorPlan = QuotaAlertCursorPlan,
            OpenAiCursor = QuotaAlertOpenAiCursor,
            OpenAiPlatform = QuotaAlertOpenAiPlatform,
            ClaudeCursor = QuotaAlertClaudeCursor,
            ClaudeApi = QuotaAlertClaudeApi,
            GeminiCursor = QuotaAlertGeminiCursor,
            OpenRouterKeyLimit = QuotaAlertOpenRouterKeyLimit,
            OpenCodeZenMonthly = QuotaAlertOpenCodeZenMonthly,
            OpenCodeGoMonthly = QuotaAlertOpenCodeGoMonthly
        };
        settings.SettingsExpandedProvider = ExpandedProvider;
    }

    public void UpdateCursorConnectionStatus()
    {
        var tokens = _cursorTokenReader();
        CursorStatus = string.IsNullOrWhiteSpace(tokens.AccessToken)
            ? "Not signed in to Cursor"
            : "Connected via Cursor session";
        UpdateConnectionStates();
    }

    public void UpdateConnectionStates()
    {
        var hasCursorToken = !string.IsNullOrWhiteSpace(_cursorTokenReader().AccessToken);

        _cursorConnectionState = ProviderConnectionStateHelper.FromConnected(
            ShowCursor, hasCursorToken);

        var openAiCursor = ProviderConnectionStateHelper.FromConnected(ShowOpenAi, hasCursorToken);
        var openAiDirect = ProviderConnectionStateHelper.FromConnected(
            ShowOpenAiDirect,
            HasOpenAiApiKeySaved);
        var openAiCodex = ProviderConnectionStateHelper.FromConnected(
            ShowCodexLimits,
            HasCodexAutoAuth || HasOpenAiSessionCookieSaved);
        _openAiHeaderState = ProviderConnectionStateHelper.Aggregate(
            openAiCursor, openAiDirect, openAiCodex);

        var claudeCursor = ProviderConnectionStateHelper.FromConnected(ShowClaude, hasCursorToken);
        var claudePro = ProviderConnectionStateHelper.FromConnected(
            ShowClaudePro,
            HasClaudeProAuth);
        var claudeApi = ProviderConnectionStateHelper.FromConnected(
            ShowClaudeApiConsole,
            HasClaudeApiKeySaved);
        _claudeHeaderState = ProviderConnectionStateHelper.Aggregate(
            claudeCursor, claudePro, claudeApi);

        var geminiCursor = ProviderConnectionStateHelper.FromConnected(ShowGemini, hasCursorToken);
        var antigravityTokens = _antigravityTokenReader();
        var hasAntigravity = !string.IsNullOrWhiteSpace(antigravityTokens.AccessToken)
                             || !string.IsNullOrWhiteSpace(antigravityTokens.RefreshToken);
        var geminiAntigravity = ProviderConnectionStateHelper.FromConnected(
            ShowAntigravityLimits, hasAntigravity);
        _geminiHeaderState = ProviderConnectionStateHelper.Aggregate(
            geminiCursor, geminiAntigravity);

        _openRouterHeaderState = ProviderConnectionStateHelper.FromConnected(
            ShowOpenRouterLimits,
            HasOpenRouterApiKeySaved);

        _openCodeHeaderState = ProviderConnectionStateHelper.Aggregate(
            ProviderConnectionStateHelper.FromConnected(ShowOpenCodeZen, HasOpenCodeSessionSaved && !string.IsNullOrWhiteSpace(OpenCodeWorkspaceId)),
            ProviderConnectionStateHelper.FromConnected(ShowOpenCodeGo, HasOpenCodeSessionSaved && !string.IsNullOrWhiteSpace(OpenCodeWorkspaceId)));

        ClaudeCursorPlanStatus = hasCursorToken
            ? "Connected via Cursor session"
            : "Sign in to Cursor IDE on this machine";

        OpenAiCursorPlanStatus = hasCursorToken
            ? "Connected via Cursor session"
            : "Sign in to Cursor IDE on this machine";

        GeminiCursorPlanStatus = hasCursorToken
            ? "Connected via Cursor session"
            : "Sign in to Cursor IDE on this machine";

        OnPropertyChanged(nameof(CursorHeaderColor));
        OnPropertyChanged(nameof(OpenAiHeaderColor));
        OnPropertyChanged(nameof(ClaudeHeaderColor));
        OnPropertyChanged(nameof(GeminiHeaderColor));
        OnPropertyChanged(nameof(OpenRouterHeaderColor));
        OnPropertyChanged(nameof(OpenCodeHeaderColor));
        OnPropertyChanged(nameof(DiskHeaderColor));
    }

    public void UpdateStatusFromSettings(WidgetSettings settings)
    {
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? OpenAiStatus;
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? CodexStatus;
        ClaudeProStatus = settings.Claude.ProLastConnectionStatus ?? ClaudeProStatus;
        ClaudeApiConsoleStatus = settings.Claude.LastConnectionStatus ?? ClaudeApiConsoleStatus;
        AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? AntigravityStatus;
        OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? OpenRouterStatus;
        OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? OpenCodeStatus;
    }

    public void SaveOpenAiApiKey(string? text)
    {
        SaveCredential("openai", ref _openAiCredentialId, text);
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void SaveOpenAiSessionCookie(string? text)
    {
        SaveProSessionCredential("openai-codex", ref _openAiProSessionCredentialId, text);
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void SaveClaudeApiKey(string? text)
    {
        SaveCredential("claude", ref _claudeCredentialId, text);
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void ClearOpenAiApiKey()
    {
        CredentialStore.Delete(_openAiCredentialId);
        _openAiCredentialId = null;
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void ClearOpenAiSessionCookie()
    {
        CredentialStore.Delete(_openAiProSessionCredentialId);
        _openAiProSessionCredentialId = null;
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void ClearClaudeApiKey()
    {
        CredentialStore.Delete(_claudeCredentialId);
        _claudeCredentialId = null;
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void SaveOpenRouterApiKey(string? text)
    {
        SaveCredential("openrouter", ref _openRouterCredentialId, text);
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void ClearOpenRouterApiKey()
    {
        CredentialStore.Delete(_openRouterCredentialId);
        _openRouterCredentialId = null;
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void SaveOpenCodeSessionCookie(string? text)
    {
        SaveProSessionCredential("opencode", ref _openCodeProSessionCredentialId, text);
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public void ClearOpenCodeSessionCookie()
    {
        CredentialStore.Delete(_openCodeProSessionCredentialId);
        _openCodeProSessionCredentialId = null;
        UpdateCredentialWatermarks();
        NotifyFieldChanged();
    }

    public async Task RunEasySetupCursorAsync(WidgetSettings settings)
    {
        var result = _easySetup.SetupCursor(settings);
        CursorStatus = result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenAiAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenAiAsync(settings);
        UpdateStatusFromSettings(settings);
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? OpenAiStatus;
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? CodexStatus;
        await CompleteEasySetupAsync(settings);
    }

    public async Task RefreshClaudeProAsync(WidgetSettings settings)
    {
        settings.Claude.ShowProLimits = true;
        Commit(settings);
        ClaudeProStatus = await _claudeProBilling.RefreshAndConnectAsync(settings.Claude);
        _claudeProSessionCredentialId = settings.Claude.ProSessionCredentialId;
        UpdateCredentialWatermarks();
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupClaudeAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupClaudeAsync(settings);
        ClaudeProStatus = result.StatusMessage ?? settings.Claude.ProLastConnectionStatus ?? "";
        UpdateStatusFromSettings(settings);
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGeminiAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupGeminiAsync(settings);
        AntigravityStatus = result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public void RunEasySetupDisk(WidgetSettings settings)
    {
        _easySetup.SetupDisk(settings);
        ShowDiskDrives = true;
        ShowDiskDetails = true;
        Commit(settings);
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenAiAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_openAiCredentialId);
        OpenAiStatus = await _openAiBilling.TestConnectionAsync(key ?? "", settings.OpenAi.OrganizationId);
        settings.OpenAi.LastConnectionStatus = OpenAiStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestCodexAsync(WidgetSettings settings)
    {
        Commit(settings);
        var session = CredentialStore.Retrieve(_openAiProSessionCredentialId);
        CodexStatus = await _codexBilling.TestConnectionAsync(session);
        settings.OpenAi.ProLastConnectionStatus = CodexStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestClaudeApiConsoleAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_claudeCredentialId);
        ClaudeApiConsoleStatus = await _anthropicBilling.TestConnectionAsync(key ?? "");
        settings.Claude.LastConnectionStatus = ClaudeApiConsoleStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestAntigravityAsync(WidgetSettings settings)
    {
        Commit(settings);
        AntigravityStatus = await _antigravityBilling.TestConnectionAsync();
        settings.Gemini.ProLastConnectionStatus = AntigravityStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenRouterAsync(WidgetSettings settings)
    {
        Commit(settings);
        var key = CredentialStore.Retrieve(_openRouterCredentialId);
        OpenRouterStatus = await _openRouterBilling.TestConnectionAsync(key ?? "");
        settings.OpenRouter.LastConnectionStatus = OpenRouterStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenCodeAsync(WidgetSettings settings)
    {
        Commit(settings);
        var session = CredentialStore.Retrieve(_openCodeProSessionCredentialId);
        OpenCodeStatus = await _openCodeBilling.TestConnectionAsync(session ?? "", OpenCodeWorkspaceId);
        settings.OpenCode.ProLastConnectionStatus = OpenCodeStatus;
        _host?.OnSettingsChanged();
    }

    public async Task RunEasySetupOpenRouterAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenRouterAsync(settings);
        OpenRouterStatus = result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenCodeAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenCodeAsync(settings);
        OpenCodeStatus = result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public void OpenClaudeAi() => new ExternalSetupLauncher().OpenClaudeAi();

    public void OpenOpenCode() => new ExternalSetupLauncher().OpenOpenCode();

    private async Task CompleteEasySetupAsync(WidgetSettings settings)
    {
        Load(settings);
        Commit(settings);
        if (_host is not null)
            await _host.OnEasySetupCompletedAsync();
    }

    private bool SetToggle(ref bool field, bool value)
    {
        if (!SetProperty(ref field, value) || _suppressChangeNotifications)
            return field == value;

        _host?.OnSettingsChanged();
        return true;
    }

    private void NotifyFieldChanged()
    {
        if (_suppressChangeNotifications)
            return;

        _host?.OnSettingsChanged();
    }

    private void UpdateCredentialWatermarks()
    {
        OpenAiApiKeyWatermark = BuildWatermark("Admin API key", _openAiCredentialId);
        OpenAiSessionCookieWatermark = BuildWatermark("ChatGPT session cookie (optional fallback)", _openAiProSessionCredentialId);
        ClaudeApiKeyWatermark = BuildWatermark("Admin API key (sk-ant-admin...)", _claudeCredentialId);
        OpenRouterApiKeyWatermark = BuildWatermark("API key (sk-or-...)", _openRouterCredentialId);
        OpenCodeSessionWatermark = BuildWatermark("opencode.ai auth cookie", _openCodeProSessionCredentialId);
        OnPropertyChanged(nameof(HasOpenAiApiKeySaved));
        OnPropertyChanged(nameof(HasOpenAiSessionCookieSaved));
        OnPropertyChanged(nameof(HasClaudeSessionCookieSaved));
        OnPropertyChanged(nameof(HasClaudeProAuth));
        OnPropertyChanged(nameof(HasClaudeApiKeySaved));
        OnPropertyChanged(nameof(HasOpenRouterApiKeySaved));
        OnPropertyChanged(nameof(HasOpenCodeSessionSaved));
        OnPropertyChanged(nameof(ShowOpenAiDirectCredentials));
        OnPropertyChanged(nameof(ShowCodexCredentials));
        OnPropertyChanged(nameof(ShowClaudeApiConsoleCredentials));
        OnPropertyChanged(nameof(ShowOpenRouterCredentials));
        OnPropertyChanged(nameof(HasCodexAutoAuth));
        UpdateConnectionStates();
    }

    private void NotifyAccordionPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsCursorExpanded));
        OnPropertyChanged(nameof(IsOpenAiExpanded));
        OnPropertyChanged(nameof(IsClaudeExpanded));
        OnPropertyChanged(nameof(IsGeminiExpanded));
        OnPropertyChanged(nameof(IsOpenRouterExpanded));
        OnPropertyChanged(nameof(IsOpenCodeExpanded));
        OnPropertyChanged(nameof(IsDiskExpanded));
        OnPropertyChanged(nameof(IsWidgetExpanded));
        OnPropertyChanged(nameof(CursorChevron));
        OnPropertyChanged(nameof(OpenAiChevron));
        OnPropertyChanged(nameof(ClaudeChevron));
        OnPropertyChanged(nameof(GeminiChevron));
        OnPropertyChanged(nameof(OpenRouterChevron));
        OnPropertyChanged(nameof(OpenCodeChevron));
        OnPropertyChanged(nameof(DiskChevron));
        OnPropertyChanged(nameof(WidgetChevron));
    }

    private static string BuildWatermark(string label, string? credentialId) =>
        string.IsNullOrWhiteSpace(credentialId) ? label : $"{label} — saved";

    private static void SaveCredential(string provider, ref string? credentialId, string? text)
    {
        var id = credentialId;
        CredentialStore.Replace(provider, credentialId, text?.Trim() ?? "", newId => id = newId);
        credentialId = id;
    }

    private static void SaveProSessionCredential(string provider, ref string? credentialId, string? text)
    {
        SaveCredential(provider, ref credentialId, text);
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static decimal? ParseBudget(string? text) =>
        decimal.TryParse(text?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : null;

    private static string FormatBudget(decimal? value) =>
        value is > 0 ? value.Value.ToString(CultureInfo.InvariantCulture) : "";
}

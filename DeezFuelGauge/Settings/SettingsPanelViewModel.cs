using System.Collections.ObjectModel;
using System.Globalization;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

public sealed class SettingsPanelViewModel : ViewModelBase
{
    private readonly ProviderEasySetupService _easySetup;
    private readonly OpenAiBillingClient _openAiBilling;
    private readonly CodexUsageClient _codexBilling;
    private readonly CodexAuthResolver _codexAuthResolver;
    private readonly AntigravityUsageClient _antigravityBilling;
    private readonly OpenRouterUsageClient _openRouterBilling;
    private readonly OpenCodeUsageClient _openCodeBilling;
    private readonly OpenCodeAuthResolver _openCodeAuthResolver;
    private readonly GeminiAuthResolver _geminiAuthResolver;
    private readonly Func<CursorTokens> _cursorTokenReader;
    private ISettingsPanelHost? _host;
    private bool _suppressChangeNotifications;
    private bool _updatingDerivedState;
    private SettingsExpandedProvider _expandedProvider = SettingsExpandedProvider.None;

    private string? _openAiCredentialId;
    private string? _openAiProSessionCredentialId;
    private string? _openRouterCredentialId;
    private string? _openRouterManagementCredentialId;
    private string? _openCodeProSessionCredentialId;
    private string? _openCodeWorkspaceId;

    public SettingsPanelViewModel(
        ProviderEasySetupService easySetup,
        OpenAiBillingClient openAiBilling,
        CodexUsageClient codexBilling,
        AntigravityUsageClient antigravityBilling,
        OpenRouterUsageClient openRouterBilling,
        OpenCodeUsageClient openCodeBilling,
        Func<CursorTokens>? cursorTokenReader = null,
        GeminiAuthResolver? geminiAuthResolver = null)
    {
        _easySetup = easySetup;
        _openAiBilling = openAiBilling;
        _codexBilling = codexBilling;
        _codexAuthResolver = new CodexAuthResolver(
            authFileReader: () => CodexUsageClient.TryReadLocalAuthFile(out var auth, null) ? auth : null);
        _antigravityBilling = antigravityBilling;
        _openRouterBilling = openRouterBilling;
        _openCodeBilling = openCodeBilling;
        _openCodeAuthResolver = new OpenCodeAuthResolver();
        _cursorTokenReader = cursorTokenReader ?? CursorTokenReader.Read;
        _geminiAuthResolver = geminiAuthResolver ?? new GeminiAuthResolver();
    }

    public ObservableCollection<ProviderSettingsSectionViewModel> Sections { get; } = [];

    public void AttachHost(ISettingsPanelHost host) => _host = host;

    public SettingsExpandedProvider ExpandedProvider
    {
        get => _expandedProvider;
        set
        {
            if (!SetProperty(ref _expandedProvider, value))
                return;

            foreach (var section in Sections)
                section.IsExpanded = section.ProviderId == value;

            _host?.OnSettingsLayoutChanged();
        }
    }

    public bool IsCursorExpanded => ExpandedProvider == SettingsExpandedProvider.Cursor;
    public bool IsOpenAiExpanded => ExpandedProvider == SettingsExpandedProvider.OpenAi;
    public bool IsGeminiExpanded => ExpandedProvider == SettingsExpandedProvider.Gemini;
    public bool IsOpenRouterExpanded => ExpandedProvider == SettingsExpandedProvider.OpenRouter;
    public bool IsOpenCodeExpanded => ExpandedProvider == SettingsExpandedProvider.OpenCode;
    public bool IsDiskExpanded => ExpandedProvider == SettingsExpandedProvider.Disk;
    public bool IsHardwareExpanded => ExpandedProvider == SettingsExpandedProvider.Hardware;

    public bool ShowCursor
    {
        get => GetCursorMain().IsEnabled;
        set => SetSourceEnabled(GetCursorMain(), value);
    }

    public bool ShowCursorDetails
    {
        get => GetCursorMain().ShowDetails;
        set => SetSourceDetail(GetCursorMain(), value);
    }

    public bool ShowBreakdown
    {
        get => GetCursorBreakdown().ShowBreakdown;
        set
        {
            var breakdown = GetCursorBreakdown();
            if (SetPropertyOnSource(breakdown, breakdown.ShowBreakdown, value, nameof(ProviderSourceViewModel.ShowBreakdown)))
                NotifyChanged();
        }
    }

    public string CursorStatus
    {
        get => GetCursorMain().Status;
        set => SetSourceStatus(GetCursorMain(), value);
    }

    public bool ShowOpenAi
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public bool ShowOpenAiDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public bool ShowOpenAiDirect
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiDirect), value);
    }

    public bool ShowOpenAiDirectDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiDirect), value);
    }

    public bool ShowCodexLimits
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowCodexDetails
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowCodexBreakdown
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiCodex);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string OpenAiOrgId
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).OrgId;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiDirect);
            if (source.OrgId != value)
            {
                source.OrgId = value;
                NotifyChanged();
            }
        }
    }

    public string OpenAiBudget
    {
        get => GetSource(ProviderSourceKind.OpenAiDirect).Budget;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenAiDirect);
            if (source.Budget != value)
            {
                source.Budget = value;
                NotifyChanged();
            }
        }
    }

    public string OpenAiStatus
    {
        get => GetSource(ProviderSourceKind.OpenAiViaCursor).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenAiViaCursor), value);
    }

    public string CodexStatus
    {
        get => GetSource(ProviderSourceKind.OpenAiCodex).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenAiCodex), value);
    }

    public bool ShowGemini
    {
        get => GetSource(ProviderSourceKind.GeminiViaCursor).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.GeminiViaCursor), value);
    }

    public bool ShowGeminiDetails
    {
        get => GetSource(ProviderSourceKind.GeminiViaCursor).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.GeminiViaCursor), value);
    }

    public bool ShowAntigravityLimits
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowAntigravityDetails
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowAntigravityBreakdown
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.AntigravityLimits);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string AntigravityStatus
    {
        get => GetSource(ProviderSourceKind.AntigravityLimits).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.AntigravityLimits), value);
    }

    public bool ShowOpenRouterLimits
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            && GetSource(ProviderSourceKind.OpenRouterCredits).IsEnabled;
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceEnabled(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public bool ShowOpenRouterDetails
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            && GetSource(ProviderSourceKind.OpenRouterCredits).ShowDetails;
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceDetail(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public string OpenRouterStatus
    {
        get => ProviderFeatureFlags.OpenRouterEnabled
            ? GetSource(ProviderSourceKind.OpenRouterCredits).Status
            : "";
        set
        {
            if (ProviderFeatureFlags.OpenRouterEnabled)
                SetSourceStatus(GetSource(ProviderSourceKind.OpenRouterCredits), value);
        }
    }

    public bool ShowOpenCodeZen
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowOpenCodeGo
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.OpenCodeGo), value);
    }

    public bool ShowOpenCodeZenDetails
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowOpenCodeGoDetails
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.OpenCodeGo), value);
    }

    public bool ShowOpenCodeGoBreakdown
    {
        get => GetSource(ProviderSourceKind.OpenCodeGo).ShowLimits;
        set
        {
            var source = GetSource(ProviderSourceKind.OpenCodeGo);
            if (SetPropertyOnSource(source, source.ShowLimits, value, nameof(ProviderSourceViewModel.ShowLimits)))
                NotifyChanged();
        }
    }

    public string OpenCodeWorkspaceId
    {
        get => Sections.Count > 0
            ? GetSource(ProviderSourceKind.OpenCodeGo).WorkspaceId
            : (_openCodeWorkspaceId ?? "");
        set
        {
            _openCodeWorkspaceId = value;
            if (Sections.Count == 0)
                return;

            var source = GetSource(ProviderSourceKind.OpenCodeGo);
            if (source.WorkspaceId != value)
            {
                source.WorkspaceId = value;
                NotifyChanged();
            }
        }
    }

    public string OpenCodeStatus
    {
        get => GetSource(ProviderSourceKind.OpenCodeZen).Status;
        set => SetSourceStatus(GetSource(ProviderSourceKind.OpenCodeZen), value);
    }

    public bool ShowDiskDrives
    {
        get => GetSource(ProviderSourceKind.DiskDrives).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.DiskDrives), value);
    }

    public bool ShowDiskDetails
    {
        get => GetSource(ProviderSourceKind.DiskDrives).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.DiskDrives), value);
    }

    public bool ShowCpuUsage
    {
        get => GetSource(ProviderSourceKind.HardwareCpuUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareCpuUsage), value);
    }

    public bool ShowGpuUsage
    {
        get => GetSource(ProviderSourceKind.HardwareGpuUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareGpuUsage), value);
    }

    public bool ShowRamUsage
    {
        get => GetSource(ProviderSourceKind.HardwareRamUsage).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareRamUsage), value);
    }

    public bool ShowCpuTemp
    {
        get => GetSource(ProviderSourceKind.HardwareCpuTemp).IsEnabled;
        set => SetSourceEnabled(GetSource(ProviderSourceKind.HardwareCpuTemp), value);
    }

    public bool ShowCpuTempDetail
    {
        get => GetSource(ProviderSourceKind.HardwareCpuTemp).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.HardwareCpuTemp), value);
    }

    public bool ShowHardwareDetails
    {
        get => GetSource(ProviderSourceKind.HardwareRamUsage).ShowDetails;
        set => SetSourceDetail(GetSource(ProviderSourceKind.HardwareRamUsage), value);
    }

    public string OpenAiApiKeyWatermark => BuildWatermark("Admin API key", _openAiCredentialId);
    public string OpenAiSessionCookieWatermark => BuildWatermark("ChatGPT session cookie", _openAiProSessionCredentialId);
    public string OpenRouterApiKeyWatermark => BuildWatermark("API key (sk-or-...)", _openRouterCredentialId);
    public string OpenRouterManagementApiKeyWatermark =>
        BuildWatermark("Management key (optional, for balance)", _openRouterManagementCredentialId);
    public string OpenCodeSessionWatermark => BuildWatermark("opencode.ai auth cookie", _openCodeProSessionCredentialId);

    public bool HasOpenAiApiKeySaved => !string.IsNullOrWhiteSpace(_openAiCredentialId);
    public bool HasOpenAiSessionCookieSaved => !string.IsNullOrWhiteSpace(_openAiProSessionCredentialId);
    public bool HasOpenRouterApiKeySaved => !string.IsNullOrWhiteSpace(_openRouterCredentialId);
    public bool HasOpenRouterManagementApiKeySaved => !string.IsNullOrWhiteSpace(_openRouterManagementCredentialId);
    public bool HasOpenCodeSessionSaved => !string.IsNullOrWhiteSpace(_openCodeProSessionCredentialId);

    public bool HasOpenCodeAutoAuth =>
        _openCodeAuthResolver.HasDetectableAuth(CreateOpenCodeBillingSettings());

    public bool HasOpenCodeAutoAuthFor(ProviderBillingSettings openCode) =>
        _openCodeAuthResolver.HasDetectableAuth(openCode);

    public bool HasOpenCodeApiKeyAuth => _openCodeAuthResolver.HasApiKeyAuth();

    public string OpenCodeAutoAuthSummary => OpenCodeAutoAuthSummaryFor(CreateOpenCodeBillingSettings());

    public string OpenCodeAutoAuthSummaryFor(ProviderBillingSettings openCode) => HasOpenCodeAutoAuthFor(openCode)
        ? (_openCodeAuthResolver.Resolve(openCode, tryBrowserCookies: false).Source switch
        {
            OpenCodeAuthSource.ApiKey => "Signed in via OpenCode CLI",
            OpenCodeAuthSource.SavedSession => "Signed in via saved session",
            _ => "Signed in via browser"
        })
        : "";

    public bool ShowOpenCodeCredentials =>
        (ShowOpenCodeZen || ShowOpenCodeGo)
        && !HasOpenCodeAutoAuth
        && !HasOpenCodeSessionSaved;

    public bool ShowOpenCodeCredentialsFor(ProviderBillingSettings openCode) =>
        (openCode.ShowDirectSource || openCode.ShowProLimits)
        && !HasOpenCodeAutoAuthFor(openCode)
        && !HasOpenCodeSessionSaved;

    public bool HasCodexAutoAuth => _codexAuthResolver.HasDetectableAuth(CreateOpenAiBillingSettings());

    public string CodexAutoAuthSummary => HasCodexAutoAuth
        ? (_codexAuthResolver.Resolve(CreateOpenAiBillingSettings(), tryBrowserCookies: false).Source switch
        {
            CodexAuthSource.AuthFile => "Signed in via Codex CLI",
            CodexAuthSource.SavedSession => "Signed in via saved session",
            _ => "Signed in via browser"
        })
        : "";

    public bool ShowCodexCredentials =>
        ShowCodexLimits && !HasCodexAutoAuth && !HasOpenAiSessionCookieSaved;

    public bool ShowOpenAiDirectCredentials => ShowOpenAiDirect && !HasOpenAiApiKeySaved;

    public bool HasGeminiAutoAuth => _geminiAuthResolver.HasDetectableAuth();

    public string GeminiAutoAuthSummary => HasGeminiAutoAuth
        ? (_geminiAuthResolver.DetectedSource() switch
        {
            GeminiAuthSource.Antigravity => "Signed in via Antigravity IDE",
            GeminiAuthSource.GeminiCli => "Signed in via Gemini CLI",
            _ => "Signed in"
        })
        : "";

    public void ToggleExpandedProvider(SettingsExpandedProvider provider) =>
        ExpandedProvider = ExpandedProvider == provider ? SettingsExpandedProvider.None : provider;

    public void Load(WidgetSettings settings)
    {
        _suppressChangeNotifications = true;
        try
        {
            _openAiCredentialId = settings.OpenAi.CredentialId;
            _openAiProSessionCredentialId = settings.OpenAi.ProSessionCredentialId;
            _openRouterCredentialId = settings.OpenRouter.CredentialId;
            _openRouterManagementCredentialId = settings.OpenRouter.ManagementCredentialId;
            _openCodeProSessionCredentialId = settings.OpenCode.ProSessionCredentialId;
            _openCodeWorkspaceId = settings.OpenCode.WorkspaceId;

            SettingsSectionMapper.PopulateSections(Sections, settings, this);
            _expandedProvider = settings.SettingsExpandedProvider;

            UpdateCredentialDerivedState();
            UpdateConnectionStates();
            WireSectionNotifications();
            OnPropertyChanged(nameof(ExpandedProvider));
            NotifyAccordionPropertiesChanged();
        }
        finally
        {
            _suppressChangeNotifications = false;
        }
    }

    public void Commit(WidgetSettings settings)
    {
        SettingsSectionMapper.ApplyToSettings(Sections, settings, ExpandedProvider);

        settings.OpenAi.CredentialId = _openAiCredentialId;
        settings.OpenAi.ProSessionCredentialId = _openAiProSessionCredentialId;
        settings.OpenRouter.CredentialId = _openRouterCredentialId;
        settings.OpenRouter.ManagementCredentialId = _openRouterManagementCredentialId;
        settings.OpenCode.ProSessionCredentialId = _openCodeProSessionCredentialId;
    }

    public string ResolveCursorStatus()
    {
        var tokens = _cursorTokenReader();
        return string.IsNullOrWhiteSpace(tokens.AccessToken)
            ? "Not signed in to Cursor"
            : "Connected via Cursor session";
    }

    public void UpdateStatusFromSettings(WidgetSettings settings)
    {
        if (Sections.Count == 0)
            return;

        CursorStatus = settings.Cursor.LastConnectionStatus ?? ResolveCursorStatus();
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? OpenAiStatus;
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? CodexStatus;
        AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? AntigravityStatus;
        if (ProviderFeatureFlags.OpenRouterEnabled)
            OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? OpenRouterStatus;
        OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? OpenCodeStatus;
        UpdateConnectionStates();
    }

    public void UpdateCursorConnectionStatus()
    {
        var status = ResolveCursorStatus();
        if (string.IsNullOrWhiteSpace(GetCursorMain().Status))
            SetSourceStatus(GetCursorMain(), status, notify: false);
        UpdateConnectionStates();
    }

    public void UpdateConnectionStates()
    {
        var hasCursorToken = !string.IsNullOrWhiteSpace(_cursorTokenReader().AccessToken);

        var cursorNative = ProviderConnectionStateHelper.FromConnected(ShowCursor, hasCursorToken);
        var openAiCursor = ProviderConnectionStateHelper.FromConnected(ShowOpenAi, hasCursorToken);
        var geminiCursor = ProviderConnectionStateHelper.FromConnected(ShowGemini, hasCursorToken);
        SetSectionColor(
            SettingsExpandedProvider.Cursor,
            ProviderConnectionStateHelper.Aggregate(cursorNative, openAiCursor, geminiCursor));

        var openAiDirect = ProviderConnectionStateHelper.FromConnected(ShowOpenAiDirect, HasOpenAiApiKeySaved);
        var openAiCodex = ProviderConnectionStateHelper.FromConnected(
            ShowCodexLimits,
            HasCodexAutoAuth || HasOpenAiSessionCookieSaved);
        SetSectionColor(
            SettingsExpandedProvider.OpenAi,
            ProviderConnectionStateHelper.Aggregate(openAiDirect, openAiCodex));

        var geminiLimits = ProviderConnectionStateHelper.FromConnected(ShowAntigravityLimits, HasGeminiAutoAuth);
        SetSectionColor(
            SettingsExpandedProvider.Gemini,
            geminiLimits);

        if (ProviderFeatureFlags.OpenRouterEnabled)
        {
            SetSectionColor(
                SettingsExpandedProvider.OpenRouter,
                ProviderConnectionStateHelper.FromConnected(ShowOpenRouterLimits, HasOpenRouterApiKeySaved));
        }

        var openCodeConnected = HasOpenCodeApiKeyAuth || HasOpenCodeAutoAuth;
        SetSectionColor(
            SettingsExpandedProvider.OpenCode,
            ProviderConnectionStateHelper.Aggregate(
                ProviderConnectionStateHelper.FromConnected(ShowOpenCodeZen, openCodeConnected),
                ProviderConnectionStateHelper.FromConnected(ShowOpenCodeGo, openCodeConnected)));

        SetSectionColor(
            SettingsExpandedProvider.Disk,
            ShowDiskDrives ? ProviderConnectionState.Connected : ProviderConnectionState.Off);

        SetSectionColor(
            SettingsExpandedProvider.Hardware,
            ShowCpuUsage || ShowGpuUsage || ShowRamUsage || ShowCpuTemp
                ? ProviderConnectionState.Connected
                : ProviderConnectionState.Off);
    }

    public async Task ConnectAsync(ProviderSourceKind kind, WidgetSettings settings)
    {
        switch (kind)
        {
            case ProviderSourceKind.CursorWidget:
                await RunEasySetupCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiViaCursor:
                await RunEasySetupOpenAiViaCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiCodex:
                await RunEasySetupCodexAsync(settings);
                break;
            case ProviderSourceKind.GeminiViaCursor:
                await RunEasySetupGeminiViaCursorAsync(settings);
                break;
            case ProviderSourceKind.AntigravityLimits:
                await RunEasySetupGeminiAsync(settings);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                await RunEasySetupOpenRouterAsync(settings);
                break;
            case ProviderSourceKind.OpenCodeZen:
            case ProviderSourceKind.OpenCodeGo:
                await RunEasySetupOpenCodeAsync(settings);
                break;
        }
    }

    public async Task TestAsync(ProviderSourceKind kind, WidgetSettings settings)
    {
        Commit(settings);
        switch (kind)
        {
            case ProviderSourceKind.CursorWidget:
            case ProviderSourceKind.OpenAiViaCursor:
            case ProviderSourceKind.GeminiViaCursor:
                await TestCursorAsync(settings);
                break;
            case ProviderSourceKind.OpenAiDirect:
                await TestOpenAiAsync(settings);
                break;
            case ProviderSourceKind.OpenAiCodex:
                await TestCodexAsync(settings);
                break;
            case ProviderSourceKind.AntigravityLimits:
                await TestAntigravityAsync(settings);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                await TestOpenRouterAsync(settings);
                break;
            case ProviderSourceKind.OpenCodeZen:
            case ProviderSourceKind.OpenCodeGo:
                await TestOpenCodeAsync(settings);
                break;
        }
    }

    public void SaveApiKey(ProviderSourceKind kind, string? text)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiDirect:
                SaveCredential("openai", ref _openAiCredentialId, text);
                break;
            case ProviderSourceKind.OpenRouterCredits:
                SaveCredential("openrouter", ref _openRouterCredentialId, text);
                break;
        }

        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void SaveManagementApiKey(ProviderSourceKind kind, string? text)
    {
        if (kind != ProviderSourceKind.OpenRouterCredits)
            return;

        SaveCredential("openrouter-mgmt", ref _openRouterManagementCredentialId, text);
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void SaveSession(ProviderSourceKind kind, string? text)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiCodex:
                SaveCredential("openai-codex", ref _openAiProSessionCredentialId, text);
                break;
            case ProviderSourceKind.OpenCodeGo:
                SaveCredential("opencode", ref _openCodeProSessionCredentialId, text);
                break;
        }

        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearApiKey(ProviderSourceKind kind)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiDirect:
                CredentialStore.Delete(_openAiCredentialId);
                _openAiCredentialId = null;
                break;
            case ProviderSourceKind.OpenRouterCredits:
                CredentialStore.Delete(_openRouterCredentialId);
                _openRouterCredentialId = null;
                break;
        }

        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearManagementApiKey(ProviderSourceKind kind)
    {
        if (kind != ProviderSourceKind.OpenRouterCredits)
            return;

        CredentialStore.Delete(_openRouterManagementCredentialId);
        _openRouterManagementCredentialId = null;
        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void ClearSession(ProviderSourceKind kind)
    {
        switch (kind)
        {
            case ProviderSourceKind.OpenAiCodex:
                CredentialStore.Delete(_openAiProSessionCredentialId);
                _openAiProSessionCredentialId = null;
                break;
            case ProviderSourceKind.OpenCodeGo:
                CredentialStore.Delete(_openCodeProSessionCredentialId);
                _openCodeProSessionCredentialId = null;
                break;
        }

        UpdateCredentialDerivedState();
        NotifyChanged();
    }

    public void OnMasterEnableChanged(ProviderSettingsSectionViewModel section, bool enabled)
    {
        section.MasterEnable = enabled;
        foreach (var source in section.Sources.Where(s => s.HasEnableToggle))
            source.IsEnabled = enabled;

        if (section.ProviderId == SettingsExpandedProvider.Disk
            || section.ProviderId == SettingsExpandedProvider.Hardware)
            section.SummaryStatus = enabled ? "Enabled" : "Off";

        NotifyChanged();
    }

    public async Task RunEasySetupCursorAsync(WidgetSettings settings)
    {
        var result = _easySetup.SetupCursor(settings);
        CursorStatus = settings.Cursor.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings, preserveCursorStatus: true);
    }

    public async Task RunEasySetupOpenAiViaCursorAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenAiAsync(settings);
        OpenAiStatus = settings.OpenAi.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupCodexAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupCodexAsync(settings);
        CodexStatus = settings.OpenAi.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGeminiViaCursorAsync(WidgetSettings settings)
    {
        ShowGemini = true;
        ShowGeminiDetails = true;
        await _easySetup.SetupGeminiViaCursorAsync(settings);
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupGeminiAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupGeminiAsync(settings);
        AntigravityStatus = settings.Gemini.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenRouterAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenRouterAsync(settings);
        OpenRouterStatus = settings.OpenRouter.LastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task RunEasySetupOpenCodeAsync(WidgetSettings settings)
    {
        var result = await _easySetup.SetupOpenCodeAsync(settings);
        OpenCodeStatus = settings.OpenCode.ProLastConnectionStatus ?? result.StatusMessage ?? "";
        await CompleteEasySetupAsync(settings);
    }

    public async Task TestCursorAsync(WidgetSettings settings)
    {
        Commit(settings);
        var status = ResolveCursorStatus();
        CursorStatus = status;
        settings.Cursor.LastConnectionStatus = status;
        UpdateConnectionStates();
        _host?.OnSettingsChanged();
        await Task.CompletedTask;
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
        settings.OpenAi.ShowProLimits = true;
        CodexStatus = await _codexBilling.RefreshAndConnectAsync(settings.OpenAi);
        _openAiProSessionCredentialId = settings.OpenAi.ProSessionCredentialId;
        UpdateCredentialDerivedState();
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
        var managementKey = CredentialStore.Retrieve(_openRouterManagementCredentialId);
        OpenRouterStatus = await _openRouterBilling.TestConnectionAsync(key ?? "", managementKey);
        settings.OpenRouter.LastConnectionStatus = OpenRouterStatus;
        _host?.OnSettingsChanged();
    }

    public async Task TestOpenCodeAsync(WidgetSettings settings)
    {
        Commit(settings);
        OpenCodeStatus = await _openCodeBilling.TestConnectionAsync(settings.OpenCode);
        settings.OpenCode.ProLastConnectionStatus = OpenCodeStatus;
        _host?.OnSettingsChanged();
    }

    private async Task CompleteEasySetupAsync(WidgetSettings settings, bool preserveCursorStatus = false)
    {
        var savedCursorStatus = preserveCursorStatus ? CursorStatus : null;
        Commit(settings);
        Load(settings);
        if (preserveCursorStatus && savedCursorStatus is not null)
        {
            CursorStatus = savedCursorStatus;
            settings.Cursor.LastConnectionStatus = savedCursorStatus;
            Commit(settings);
        }

        if (_host is not null)
            await _host.OnEasySetupCompletedAsync();
    }

    private ProviderSourceViewModel GetCursorMain() =>
        GetSection(SettingsExpandedProvider.Cursor).Sources.First(s => s.HasEnableToggle);

    private ProviderSourceViewModel GetCursorBreakdown() =>
        GetSection(SettingsExpandedProvider.Cursor).Sources.First(s => s.HasBreakdownToggle);

    private ProviderSourceViewModel GetSource(ProviderSourceKind kind) =>
        Sections.SelectMany(s => s.Sources).First(s => s.Kind == kind);

    private ProviderSettingsSectionViewModel GetSection(SettingsExpandedProvider provider) =>
        Sections.First(s => s.ProviderId == provider);

    private void SetSectionColor(SettingsExpandedProvider provider, ProviderConnectionState state)
    {
        var section = GetSection(provider);
        section.HeaderColor = ProviderConnectionStateHelper.ToColor(state);
    }

    private void SetSourceEnabled(ProviderSourceViewModel source, bool value)
    {
        if (SetPropertyOnSource(source, source.IsEnabled, value, nameof(ProviderSourceViewModel.IsEnabled)))
            NotifyChanged();
    }

    private void SetSourceDetail(ProviderSourceViewModel source, bool value)
    {
        if (SetPropertyOnSource(source, source.ShowDetails, value, nameof(ProviderSourceViewModel.ShowDetails)))
            NotifyChanged();
    }

    private void SetSourceStatus(ProviderSourceViewModel source, string value, bool notify = true)
    {
        if (source.Status == value)
            return;

        source.Status = value;
        if (notify)
            NotifyChanged();
    }

    private static bool SetPropertyOnSource<T>(
        ProviderSourceViewModel source,
        T current,
        T value,
        string propertyName)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
            return false;

        switch (propertyName)
        {
            case nameof(ProviderSourceViewModel.IsEnabled):
                source.IsEnabled = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowDetails):
                source.ShowDetails = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowLimits):
                source.ShowLimits = (bool)(object)value!;
                break;
            case nameof(ProviderSourceViewModel.ShowBreakdown):
                source.ShowBreakdown = (bool)(object)value!;
                break;
        }

        return true;
    }

    private void NotifyChanged()
    {
        UpdateCredentialDerivedState();
        UpdateConnectionStates();
        if (!_suppressChangeNotifications)
            _host?.OnSettingsChanged();
    }

    private void UpdateCredentialDerivedState()
    {
        if (_updatingDerivedState || Sections.Count == 0)
            return;

        _updatingDerivedState = true;
        try
        {
            var codex = GetSource(ProviderSourceKind.OpenAiCodex);
            codex.HasAutoAuth = HasCodexAutoAuth;
            codex.AutoAuthSummary = CodexAutoAuthSummary;
            codex.ShowAdvancedSection = ShowCodexLimits && !HasCodexAutoAuth;
            codex.ShowSessionField = !HasCodexAutoAuth;
            codex.HasSessionSaved = HasOpenAiSessionCookieSaved;
            codex.SessionWatermark = OpenAiSessionCookieWatermark;
            codex.NotifyAdvancedVisibility();

            var direct = GetSource(ProviderSourceKind.OpenAiDirect);
            direct.ShowAdvancedSection = ShowOpenAiDirect;
            direct.ShowApiKeyField = !HasOpenAiApiKeySaved;
            direct.ShowOrgIdField = ShowOpenAiDirect;
            direct.ShowBudgetField = ShowOpenAiDirect;
            direct.HasApiKeySaved = HasOpenAiApiKeySaved;
            direct.ApiKeyWatermark = OpenAiApiKeyWatermark;
            direct.NotifyAdvancedVisibility();

            var geminiLimits = GetSource(ProviderSourceKind.AntigravityLimits);
            geminiLimits.HasAutoAuth = HasGeminiAutoAuth;
            geminiLimits.AutoAuthSummary = GeminiAutoAuthSummary;
            geminiLimits.ShowAdvancedSection = ShowAntigravityLimits && !HasGeminiAutoAuth;
            geminiLimits.NotifyAdvancedVisibility();

            if (ProviderFeatureFlags.OpenRouterEnabled)
            {
                var openRouter = GetSource(ProviderSourceKind.OpenRouterCredits);
                openRouter.ShowAdvancedSection = ShowOpenRouterLimits;
                openRouter.ShowApiKeyField = !HasOpenRouterApiKeySaved;
                openRouter.ShowManagementApiKeyField = ShowOpenRouterLimits && !HasOpenRouterManagementApiKeySaved;
                openRouter.HasApiKeySaved = HasOpenRouterApiKeySaved;
                openRouter.HasManagementApiKeySaved = HasOpenRouterManagementApiKeySaved;
                openRouter.ApiKeyWatermark = OpenRouterApiKeyWatermark;
                openRouter.ManagementApiKeyWatermark = OpenRouterManagementApiKeyWatermark;
                openRouter.NotifyAdvancedVisibility();
            }

            var openCodeZen = GetSource(ProviderSourceKind.OpenCodeZen);
            openCodeZen.HasAutoAuth = HasOpenCodeAutoAuth;
            openCodeZen.AutoAuthSummary = OpenCodeAutoAuthSummary;
            openCodeZen.NotifyAdvancedVisibility();

            var openCodeGo = GetSource(ProviderSourceKind.OpenCodeGo);
            openCodeGo.HasAutoAuth = HasOpenCodeAutoAuth;
            openCodeGo.AutoAuthSummary = OpenCodeAutoAuthSummary;
            openCodeGo.ShowAdvancedSection = (ShowOpenCodeZen || ShowOpenCodeGo) && !HasOpenCodeApiKeyAuth;
            openCodeGo.ShowSessionField = ShowOpenCodeCredentials;
            openCodeGo.ShowWorkspaceField = !HasOpenCodeApiKeyAuth;
            openCodeGo.HasSessionSaved = HasOpenCodeSessionSaved;
            openCodeGo.SessionWatermark = OpenCodeSessionWatermark;
            openCodeGo.NotifyAdvancedVisibility();

            OnPropertyChanged(nameof(ShowOpenCodeCredentials));
            OnPropertyChanged(nameof(HasOpenCodeAutoAuth));
            OnPropertyChanged(nameof(HasOpenCodeApiKeyAuth));

            OnPropertyChanged(nameof(ShowCodexCredentials));
            OnPropertyChanged(nameof(ShowOpenAiDirectCredentials));
            OnPropertyChanged(nameof(HasCodexAutoAuth));
            OnPropertyChanged(nameof(HasGeminiAutoAuth));
        }
        finally
        {
            _updatingDerivedState = false;
        }
    }

    private void WireSectionNotifications()
    {
        foreach (var section in Sections)
        {
            section.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(ProviderSettingsSectionViewModel.MasterEnable))
                    return;
                NotifyChanged();
            };

            foreach (var source in section.Sources)
            {
                source.PropertyChanged += (_, _) =>
                {
                    if (!_suppressChangeNotifications && !_updatingDerivedState)
                        NotifyChanged();
                };
            }
        }
    }

    private ProviderBillingSettings CreateOpenAiBillingSettings() => new()
    {
        ProSessionCredentialId = _openAiProSessionCredentialId
    };

    private ProviderBillingSettings CreateOpenCodeBillingSettings() => new()
    {
        ProSessionCredentialId = _openCodeProSessionCredentialId,
        WorkspaceId = _openCodeWorkspaceId
    };

    private void NotifyAccordionPropertiesChanged()
    {
        OnPropertyChanged(nameof(IsCursorExpanded));
        OnPropertyChanged(nameof(IsOpenAiExpanded));
        OnPropertyChanged(nameof(IsGeminiExpanded));
        OnPropertyChanged(nameof(IsOpenRouterExpanded));
        OnPropertyChanged(nameof(IsOpenCodeExpanded));
        OnPropertyChanged(nameof(IsDiskExpanded));
        OnPropertyChanged(nameof(IsHardwareExpanded));
    }

    private static string BuildWatermark(string label, string? credentialId) =>
        string.IsNullOrWhiteSpace(credentialId) ? label : $"{label} — saved";

    private static void SaveCredential(string provider, ref string? credentialId, string? text)
    {
        var id = credentialId;
        CredentialStore.Replace(provider, credentialId, text?.Trim() ?? "", newId => id = newId);
        credentialId = id;
    }
}

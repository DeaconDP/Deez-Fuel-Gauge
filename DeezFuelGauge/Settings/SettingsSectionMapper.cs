using System.Globalization;
using System.Runtime.InteropServices;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;

namespace DeezFuelGauge.Settings;

internal static class SettingsSectionMapper
{
    public static void PopulateSections(
        IList<ProviderSettingsSectionViewModel> sections,
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        sections.Clear();

        sections.Add(BuildCursorSection(settings, host));
        sections.Add(BuildOpenAiSection(settings, host));
        sections.Add(BuildGeminiSection(settings, host));
        if (ProviderFeatureFlags.OpenRouterEnabled)
            sections.Add(BuildOpenRouterSection(settings, host));
        sections.Add(BuildOpenCodeSection(settings, host));
        sections.Add(BuildDiskSection(settings, host));
        sections.Add(BuildSystemSection(settings));

        foreach (var section in sections)
            section.IsExpanded = settings.SettingsExpandedProvider == section.ProviderId;
    }

    public static void ApplyToSettings(
        IList<ProviderSettingsSectionViewModel> sections,
        WidgetSettings settings,
        SettingsExpandedProvider expandedProvider)
    {
        foreach (var section in sections)
        {
            switch (section.ProviderId)
            {
                case SettingsExpandedProvider.Cursor:
                    ApplyCursor(section, settings);
                    break;
                case SettingsExpandedProvider.OpenAi:
                    ApplyOpenAi(section, settings);
                    break;
                case SettingsExpandedProvider.Gemini:
                    ApplyGemini(section, settings);
                    break;
                case SettingsExpandedProvider.OpenRouter:
                    ApplyOpenRouter(section, settings);
                    break;
                case SettingsExpandedProvider.OpenCode:
                    ApplyOpenCode(section, settings);
                    break;
                case SettingsExpandedProvider.Disk:
                    ApplyDisk(section, settings);
                    break;
                case SettingsExpandedProvider.System:
                    ApplySystem(section, settings);
                    break;
            }
        }

        settings.SettingsExpandedProvider = expandedProvider;
    }

    private static ProviderSettingsSectionViewModel BuildCursorSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var breakdown = new ProviderSourceViewModel
        {
            Kind = ProviderSourceKind.CursorWidget,
            Name = "Auto/API breakdown",
            HasEnableToggle = false,
            HasDetailsToggle = false,
            ShowBreakdown = settings.ShowBreakdown,
            HasBreakdownToggle = true,
            ShowConnect = false,
            ShowTest = false
        };

        var main = new ProviderSourceViewModel
        {
            Kind = ProviderSourceKind.CursorWidget,
            Name = "Cursor usage",
            IsEnabled = settings.Cursor.ShowCursorSource,
            ShowDetails = settings.Cursor.ShowDetails,
            Status = settings.Cursor.LastConnectionStatus ?? host.ResolveCursorStatus(),
            ShowConnect = true,
            ShowTest = true,
            HasLimitsToggle = false
        };

        var openAiViaCursor = CreateSource(
            ProviderSourceKind.OpenAiViaCursor,
            "OpenAI (via Cursor)",
            settings.OpenAi.ShowCursorSource,
            settings.OpenAi.ShowDetails,
            settings.OpenAi.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);

        var geminiViaCursor = CreateSource(
            ProviderSourceKind.GeminiViaCursor,
            "Gemini (via Cursor)",
            settings.Gemini.ShowCursorSource,
            settings.Gemini.ShowDetails,
            "",
            showConnect: true,
            showTest: true);

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Cursor,
            Title = "Cursor",
            MasterEnable = HasAnyCursorDashboardSource(settings),
            SummaryStatus = main.Status
        };
        section.Sources.Add(main);
        section.Sources.Add(openAiViaCursor);
        section.Sources.Add(geminiViaCursor);
        section.Sources.Add(breakdown);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenAiSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var direct = CreateSource(
            ProviderSourceKind.OpenAiDirect,
            "Direct API",
            settings.OpenAi.ShowDirectSource,
            settings.OpenAi.EffectiveShowDirectDetails,
            settings.OpenAi.LastConnectionStatus ?? "",
            showConnect: false,
            showTest: true);
        direct.SupportsAdvanced = true;
        direct.ShowApiKeyField = !host.HasOpenAiApiKeySaved;
        direct.ShowOrgIdField = settings.OpenAi.ShowDirectSource;
        direct.ShowBudgetField = settings.OpenAi.ShowDirectSource;
        direct.OrgId = settings.OpenAi.OrganizationId ?? "";
        direct.Budget = FormatBudget(settings.OpenAi.MonthlyBudgetUsd);
        direct.ApiKeyWatermark = host.OpenAiApiKeyWatermark;
        direct.HasApiKeySaved = host.HasOpenAiApiKeySaved;
        direct.AdvancedHint = "Platform Admin API key with api.usage.read scope.";
        direct.ShowAdvancedSection = settings.OpenAi.ShowDirectSource;

        var codex = CreateSource(
            ProviderSourceKind.OpenAiCodex,
            "Codex",
            settings.OpenAi.ShowProLimits,
            settings.OpenAi.EffectiveShowProDetails,
            settings.OpenAi.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        codex.HasLimitsToggle = true;
        codex.ShowLimits = settings.OpenAi.ShowProBreakdown;
        codex.SupportsAdvanced = true;
        codex.HasAutoAuth = host.HasCodexAutoAuth;
        codex.AutoAuthSummary = host.CodexAutoAuthSummary;
        codex.ShowSessionField = !host.HasCodexAutoAuth;
        codex.SessionWatermark = host.OpenAiSessionCookieWatermark;
        codex.HasSessionSaved = host.HasOpenAiSessionCookieSaved;
        codex.AdvancedHint = "Uses ~/.codex/auth.json when present.";
        codex.ShowAdvancedSection = settings.OpenAi.ShowProLimits && !host.HasCodexAutoAuth;

        var section = CreateSection(
            SettingsExpandedProvider.OpenAi,
            "OpenAI",
            codex.Status,
            direct.Status);
        section.MasterEnable = settings.OpenAi.ShowDirectSource || settings.OpenAi.ShowProLimits;
        section.Sources.Add(direct);
        section.Sources.Add(codex);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildGeminiSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var antigravity = CreateSource(
            ProviderSourceKind.AntigravityLimits,
            "Gemini limits",
            settings.Gemini.ShowProLimits,
            settings.Gemini.EffectiveShowProDetails,
            settings.Gemini.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        antigravity.HasLimitsToggle = true;
        antigravity.ShowLimits = settings.Gemini.ShowProBreakdown;
        antigravity.SupportsAdvanced = true;
        antigravity.HasAutoAuth = host.HasGeminiAutoAuth;
        antigravity.AutoAuthSummary = host.GeminiAutoAuthSummary;
        antigravity.AdvancedHint = "Sign in to Antigravity IDE or run gemini login (Gemini CLI) on this machine.";
        antigravity.ShowAdvancedSection = settings.Gemini.ShowProLimits && !host.HasGeminiAutoAuth;

        var section = CreateSection(
            SettingsExpandedProvider.Gemini,
            "Gemini",
            antigravity.Status);
        section.MasterEnable = settings.Gemini.ShowProLimits;
        section.Sources.Add(antigravity);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenRouterSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var credits = CreateSource(
            ProviderSourceKind.OpenRouterCredits,
            "Credits",
            settings.OpenRouter.ShowProLimits,
            settings.OpenRouter.ShowDetails,
            settings.OpenRouter.LastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        credits.SupportsAdvanced = true;
        credits.ShowApiKeyField = !host.HasOpenRouterApiKeySaved;
        credits.ShowManagementApiKeyField = !host.HasOpenRouterManagementApiKeySaved;
        credits.ApiKeyWatermark = host.OpenRouterApiKeyWatermark;
        credits.ManagementApiKeyWatermark = host.OpenRouterManagementApiKeyWatermark;
        credits.HasApiKeySaved = host.HasOpenRouterApiKeySaved;
        credits.HasManagementApiKeySaved = host.HasOpenRouterManagementApiKeySaved;
        credits.AdvancedHint =
            "API key from openrouter.ai/settings/keys (usage and per-key limits via GET /key). " +
            "Optional management key from openrouter.ai/settings/management-keys for account balance.";
        credits.ShowAdvancedSection = settings.OpenRouter.ShowProLimits;

        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.OpenRouter,
            Title = "OpenRouter",
            MasterEnable = settings.OpenRouter.ShowProLimits,
            SummaryStatus = credits.Status
        };
        section.Sources.Add(credits);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildOpenCodeSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var zen = CreateSource(
            ProviderSourceKind.OpenCodeZen,
            "Zen",
            settings.OpenCode.ShowDirectSource,
            settings.OpenCode.ShowDetails,
            settings.OpenCode.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        zen.HasAutoAuth = host.HasOpenCodeAutoAuthFor(settings.OpenCode);
        zen.AutoAuthSummary = host.OpenCodeAutoAuthSummaryFor(settings.OpenCode);
        zen.AdvancedHint = "Uses ~/.local/share/opencode/auth.json when present.";

        var go = CreateSource(
            ProviderSourceKind.OpenCodeGo,
            "Go",
            settings.OpenCode.ShowProLimits,
            settings.OpenCode.EffectiveShowProDetails,
            settings.OpenCode.ProLastConnectionStatus ?? "",
            showConnect: true,
            showTest: true);
        go.HasLimitsToggle = true;
        go.ShowLimits = settings.OpenCode.ShowProBreakdown;
        go.HasAutoAuth = host.HasOpenCodeAutoAuthFor(settings.OpenCode);
        go.AutoAuthSummary = host.OpenCodeAutoAuthSummaryFor(settings.OpenCode);
        go.SupportsAdvanced = true;
        go.ShowWorkspaceField = !host.HasOpenCodeApiKeyAuth;
        go.ShowSessionField = host.ShowOpenCodeCredentialsFor(settings.OpenCode);
        go.WorkspaceId = settings.OpenCode.WorkspaceId ?? "";
        go.SessionWatermark = host.OpenCodeSessionWatermark;
        go.HasSessionSaved = host.HasOpenCodeSessionSaved;
        go.AdvancedHint = host.HasOpenCodeApiKeyAuth
            ? "Dashboard fallback: auth cookie + workspace ID from opencode.ai."
            : "Sign in at opencode.ai or run opencode /connect; set workspace ID if needed.";
        go.ShowAdvancedSection = (settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits)
                                 && !host.HasOpenCodeApiKeyAuth;

        var section = CreateSection(
            SettingsExpandedProvider.OpenCode,
            "OpenCode",
            zen.Status,
            go.Status);
        section.MasterEnable = settings.OpenCode.HasAnyDashboardSource;
        section.Sources.Add(zen);
        section.Sources.Add(go);
        return section;
    }

    private static ProviderSettingsSectionViewModel BuildDiskSection(
        WidgetSettings settings,
        SettingsPanelViewModel host)
    {
        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.Disk,
            Title = "Disk",
            MasterEnable = settings.ShowDiskDrives,
            SummaryStatus = BuildDiskSummaryStatus(settings)
        };

        section.Sources.Add(CreateDiskDetailsSource(settings));
        section.Sources.Add(CreateDiskAggregateSource(settings));
        AddDiskDriveSources(section, settings);
        return section;
    }

    public static void RefreshDiskDriveSources(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var existingStates = section.Sources
            .Where(s => s.Kind == ProviderSourceKind.DiskDrive && !string.IsNullOrWhiteSpace(s.DrivePath))
            .ToDictionary(s => s.DrivePath!, s => s.IsEnabled, StringComparer.Ordinal);

        foreach (var source in section.Sources.Where(s => s.Kind == ProviderSourceKind.DiskDrive).ToList())
            section.Sources.Remove(source);

        var insertIndex = 0;
        for (var i = 0; i < section.Sources.Count; i++)
        {
            if (section.Sources[i].Kind == ProviderSourceKind.DiskAggregate)
            {
                insertIndex = i + 1;
                break;
            }
        }

        foreach (var drive in DiskSpaceProvider.GetDriveDescriptors())
        {
            var enabled = existingStates.TryGetValue(drive.Name, out var state)
                ? state
                : !DiskSpaceProvider.IsDriveDisabled(settings.DisabledDiskDrives, drive.Name);

            var source = CreateDiskDriveSource(drive, enabled);
            section.Sources.Insert(insertIndex++, source);
        }

        section.SummaryStatus = BuildDiskSummaryStatus(settings, section);
    }

    private static ProviderSourceViewModel CreateDiskDetailsSource(WidgetSettings settings) => new()
    {
        Kind = ProviderSourceKind.DiskDetails,
        Name = "Usage details",
        HasEnableToggle = false,
        HasDetailsToggle = true,
        ShowDetails = settings.ShowDiskDetails,
        ShowConnect = false,
        ShowTest = false
    };

    private static ProviderSourceViewModel CreateDiskAggregateSource(WidgetSettings settings)
    {
        var aggregate = new ProviderSourceViewModel
        {
            Kind = ProviderSourceKind.DiskAggregate,
            Name = "Aggregate volumes",
            IsEnabled = settings.DiskAggregateVolumes,
            HasDetailsToggle = false,
            ShowConnect = false,
            ShowTest = false
        };

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            aggregate.Status =
                "APFS volumes may share the same physical storage; disable duplicate mounts if totals look too high.";
        }

        return aggregate;
    }

    private static ProviderSourceViewModel CreateDiskDriveSource(DiskDriveDescriptor drive, bool enabled) => new()
    {
        Kind = ProviderSourceKind.DiskDrive,
        Name = drive.DisplayLabel,
        DrivePath = drive.Name,
        IsEnabled = enabled,
        HasDetailsToggle = false,
        ShowConnect = false,
        ShowTest = false
    };

    private static void AddDiskDriveSources(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        foreach (var drive in DiskSpaceProvider.GetDriveDescriptors())
        {
            var enabled = !DiskSpaceProvider.IsDriveDisabled(settings.DisabledDiskDrives, drive.Name);
            section.Sources.Add(CreateDiskDriveSource(drive, enabled));
        }
    }

    private static string BuildDiskSummaryStatus(WidgetSettings settings) =>
        BuildDiskSummaryStatus(settings, null);

    public static string BuildDiskSummaryStatus(WidgetSettings settings, ProviderSettingsSectionViewModel? section)
    {
        if (!settings.ShowDiskDrives)
            return "Off";

        if (settings.DiskAggregateVolumes)
            return "Aggregated";

        var enabledCount = section?.Sources.Count(s => s.Kind == ProviderSourceKind.DiskDrive && s.IsEnabled)
            ?? DiskSpaceProvider.GetEnabledDrives(settings).Count;

        return enabledCount switch
        {
            0 => "No drives",
            1 => "1 drive",
            _ => $"{enabledCount} drives"
        };
    }

    private static ProviderSourceViewModel CreateSource(
        ProviderSourceKind kind,
        string name,
        bool enabled,
        bool details,
        string status,
        bool showConnect,
        bool showTest) => new()
    {
        Kind = kind,
        Name = name,
        IsEnabled = enabled,
        ShowDetails = details,
        Status = status,
        ShowConnect = showConnect,
        ShowTest = showTest
    };

    private static ProviderSettingsSectionViewModel CreateSection(
        SettingsExpandedProvider id,
        string title,
        params string[] statuses)
    {
        var summary = statuses.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? "";
        return new ProviderSettingsSectionViewModel
        {
            ProviderId = id,
            Title = title,
            SummaryStatus = summary
        };
    }

    private static bool HasAnyCursorDashboardSource(WidgetSettings settings) =>
        settings.Cursor.ShowCursorSource
        || settings.OpenAi.ShowCursorSource
        || settings.Gemini.ShowCursorSource;

    private static void ApplyCursor(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var main = section.Sources.First(s => s.Kind == ProviderSourceKind.CursorWidget && s.HasEnableToggle);
        var openAiViaCursor = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiViaCursor);
        var geminiViaCursor = section.Sources.First(s => s.Kind == ProviderSourceKind.GeminiViaCursor);
        var breakdown = section.Sources.First(s => s.HasBreakdownToggle);

        settings.Cursor.ShowCursorSource = main.IsEnabled;
        settings.Cursor.ShowDetails = main.ShowDetails;
        settings.Cursor.LastConnectionStatus = NullIfEmpty(main.Status);
        settings.OpenAi.ShowCursorSource = openAiViaCursor.IsEnabled;
        settings.OpenAi.ShowDetails = openAiViaCursor.ShowDetails;
        settings.OpenAi.LastConnectionStatus = NullIfEmpty(openAiViaCursor.Status);
        settings.Gemini.ShowCursorSource = geminiViaCursor.IsEnabled;
        settings.Gemini.ShowDetails = geminiViaCursor.ShowDetails;
        settings.ShowBreakdown = breakdown.ShowBreakdown;
        section.MasterEnable = HasAnyCursorDashboardSource(settings);
    }

    private static void ApplyOpenAi(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var direct = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiDirect);
        var codex = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenAiCodex);

        settings.OpenAi.ShowDirectSource = direct.IsEnabled;
        settings.OpenAi.ShowDirectDetails = direct.ShowDetails;
        settings.OpenAi.OrganizationId = NullIfEmpty(direct.OrgId);
        settings.OpenAi.MonthlyBudgetUsd = ParseBudget(direct.Budget);
        settings.OpenAi.ShowProLimits = codex.IsEnabled;
        settings.OpenAi.ShowProDetails = codex.ShowDetails;
        settings.OpenAi.ShowProBreakdown = codex.ShowLimits;
        settings.OpenAi.ProLastConnectionStatus = NullIfEmpty(codex.Status);
        section.MasterEnable = settings.OpenAi.ShowDirectSource || settings.OpenAi.ShowProLimits;
    }

    private static void ApplyGemini(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var antigravity = section.Sources.First(s => s.Kind == ProviderSourceKind.AntigravityLimits);

        settings.Gemini.ShowProLimits = antigravity.IsEnabled;
        settings.Gemini.ShowProDetails = antigravity.ShowDetails;
        settings.Gemini.ShowProBreakdown = antigravity.ShowLimits;
        settings.Gemini.ProLastConnectionStatus = NullIfEmpty(antigravity.Status);
        section.MasterEnable = settings.Gemini.ShowProLimits;
    }

    private static void ApplyOpenRouter(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var credits = section.Sources.Single();
        settings.OpenRouter.ShowProLimits = credits.IsEnabled;
        settings.OpenRouter.ShowDetails = credits.ShowDetails;
        settings.OpenRouter.LastConnectionStatus = NullIfEmpty(credits.Status);
        section.MasterEnable = credits.IsEnabled;
    }

    private static void ApplyOpenCode(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var zen = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenCodeZen);
        var go = section.Sources.First(s => s.Kind == ProviderSourceKind.OpenCodeGo);

        settings.OpenCode.ShowDirectSource = zen.IsEnabled;
        settings.OpenCode.ShowDetails = zen.ShowDetails;
        settings.OpenCode.ShowProLimits = go.IsEnabled;
        settings.OpenCode.ShowProDetails = go.ShowDetails;
        settings.OpenCode.ShowProBreakdown = go.ShowLimits;
        settings.OpenCode.WorkspaceId = NullIfEmpty(go.WorkspaceId);
        settings.OpenCode.ProLastConnectionStatus = NullIfEmpty(zen.Status) ?? NullIfEmpty(go.Status);
        section.MasterEnable = settings.OpenCode.HasAnyDashboardSource;
    }

    private static void ApplyDisk(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var details = section.Sources.First(s => s.Kind == ProviderSourceKind.DiskDetails);
        var aggregate = section.Sources.First(s => s.Kind == ProviderSourceKind.DiskAggregate);

        settings.ShowDiskDrives = section.MasterEnable;
        settings.ShowDiskDetails = details.ShowDetails;
        settings.DiskAggregateVolumes = aggregate.IsEnabled;
        settings.DisabledDiskDrives = section.Sources
            .Where(s => s.Kind == ProviderSourceKind.DiskDrive && !string.IsNullOrWhiteSpace(s.DrivePath) && !s.IsEnabled)
            .Select(s => s.DrivePath!)
            .ToList();

        section.SummaryStatus = BuildDiskSummaryStatus(settings, section);
    }

    private static ProviderSettingsSectionViewModel BuildSystemSection(WidgetSettings settings)
    {
        var section = new ProviderSettingsSectionViewModel
        {
            ProviderId = SettingsExpandedProvider.System,
            Title = "System",
            MasterEnable = settings.ShowSystemResources,
            SummaryStatus = BuildSystemSummaryStatus(settings)
        };

        section.Sources.Add(CreateSystemDetailsSource(settings));
        section.Sources.Add(CreateSystemMetricSource(ProviderSourceKind.SystemRam, "RAM", settings.ShowRam));
        section.Sources.Add(CreateSystemMetricSource(ProviderSourceKind.SystemCpu, "CPU", settings.ShowCpu));
        section.Sources.Add(CreateSystemGpuSource(settings));
        return section;
    }

    private static ProviderSourceViewModel CreateSystemDetailsSource(WidgetSettings settings) => new()
    {
        Kind = ProviderSourceKind.SystemDetails,
        Name = "Usage details",
        HasEnableToggle = false,
        HasDetailsToggle = true,
        ShowDetails = settings.ShowSystemDetails,
        ShowConnect = false,
        ShowTest = false
    };

    private static ProviderSourceViewModel CreateSystemMetricSource(
        ProviderSourceKind kind,
        string name,
        bool enabled) => new()
    {
        Kind = kind,
        Name = name,
        IsEnabled = enabled,
        HasDetailsToggle = false,
        ShowConnect = false,
        ShowTest = false
    };

    private static ProviderSourceViewModel CreateSystemGpuSource(WidgetSettings settings)
    {
        var gpu = CreateSystemMetricSource(ProviderSourceKind.SystemGpu, "GPU", settings.ShowGpu);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            gpu.Status = "GPU utilization is not available on macOS.";

        return gpu;
    }

    public static string BuildSystemSummaryStatus(WidgetSettings settings)
    {
        if (!settings.ShowSystemResources)
            return "Off";

        var enabled = new[]
        {
            settings.ShowRam,
            settings.ShowCpu,
            settings.ShowGpu
        }.Count(x => x);

        return enabled switch
        {
            0 => "No metrics",
            1 => "1 metric",
            _ => $"{enabled} metrics"
        };
    }

    private static void ApplySystem(ProviderSettingsSectionViewModel section, WidgetSettings settings)
    {
        var details = section.Sources.First(s => s.Kind == ProviderSourceKind.SystemDetails);
        var ram = section.Sources.First(s => s.Kind == ProviderSourceKind.SystemRam);
        var cpu = section.Sources.First(s => s.Kind == ProviderSourceKind.SystemCpu);
        var gpu = section.Sources.First(s => s.Kind == ProviderSourceKind.SystemGpu);

        settings.ShowSystemResources = section.MasterEnable;
        settings.ShowSystemDetails = details.ShowDetails;
        settings.ShowRam = ram.IsEnabled;
        settings.ShowCpu = cpu.IsEnabled;
        settings.ShowGpu = gpu.IsEnabled;
        section.SummaryStatus = BuildSystemSummaryStatus(settings);
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

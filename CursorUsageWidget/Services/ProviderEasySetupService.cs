using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class ProviderEasySetupService
{
    private readonly CodexUsageClient _codex;
    private readonly ClaudeProUsageClient _claudePro;
    private readonly AntigravityUsageClient _antigravity;
    private readonly ExternalSetupLauncher _launcher;
    private readonly Func<CursorTokens> _cursorTokenReader;
    private readonly Func<AntigravityOAuthTokens> _antigravityTokenReader;

    public ProviderEasySetupService(
        CodexUsageClient? codex = null,
        ClaudeProUsageClient? claudePro = null,
        AntigravityUsageClient? antigravity = null,
        ExternalSetupLauncher? launcher = null,
        Func<CursorTokens>? cursorTokenReader = null,
        Func<AntigravityOAuthTokens>? antigravityTokenReader = null)
    {
        _codex = codex ?? new CodexUsageClient();
        _claudePro = claudePro ?? new ClaudeProUsageClient();
        _antigravity = antigravity ?? new AntigravityUsageClient();
        _launcher = launcher ?? new ExternalSetupLauncher();
        _cursorTokenReader = cursorTokenReader ?? CursorTokenReader.Read;
        _antigravityTokenReader = antigravityTokenReader ?? AntigravityTokenReader.Read;
    }

    public EasySetupResult SetupCursor(WidgetSettings settings)
    {
        settings.Cursor.ShowCursorSource = true;
        settings.Cursor.ShowDetails = true;
        settings.ShowBreakdown = true;

        var tokens = _cursorTokenReader();
        var status = string.IsNullOrWhiteSpace(tokens.AccessToken)
            ? "Sign in to Cursor IDE on this machine"
            : "Connected via Cursor session";

        return new EasySetupResult(status);
    }

    public async Task<EasySetupResult> SetupOpenAiAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenAi.ShowCursorSource = true;
        settings.OpenAi.ShowDetails = true;
        settings.OpenAi.ShowProLimits = true;

        var cursorTokens = _cursorTokenReader();
        settings.OpenAi.LastConnectionStatus = string.IsNullOrWhiteSpace(cursorTokens.AccessToken)
            ? "Cursor spend: sign in to Cursor IDE"
            : "Cursor spend: connected";

        var hasAuthFile = _codex.HasLocalAuthFile();
        var session = CredentialStore.Retrieve(settings.OpenAi.ProSessionCredentialId);
        var hasSession = !string.IsNullOrWhiteSpace(session);

        if (!hasAuthFile && !hasSession)
        {
            _launcher.OpenChatGpt();
            const string message =
                "Codex: paste a ChatGPT session cookie in settings, or use codex login for CLI auth";
            settings.OpenAi.ProLastConnectionStatus = message;
            return new EasySetupResult(message, OpenedExternalUrl: true);
        }

        var status = await _codex.TestConnectionAsync(hasSession ? session : null, cancellationToken);
        settings.OpenAi.ProLastConnectionStatus = status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase)
            ? $"Codex: {status}"
            : status;
        return new EasySetupResult(status);
    }

    public async Task<EasySetupResult> SetupClaudeAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Claude.ShowCursorSource = true;
        settings.Claude.ShowDetails = true;
        settings.Claude.ShowProLimits = true;

        var status = await _claudePro.RefreshAndConnectAsync(settings.Claude, cancellationToken);
        if (status.StartsWith("Connected", StringComparison.OrdinalIgnoreCase))
            return new EasySetupResult(status);

        _launcher.OpenClaudeAi();
        const string message = "Sign in at claude.ai, then click Refresh";
        settings.Claude.ProLastConnectionStatus = message;
        return new EasySetupResult(message, OpenedExternalUrl: true);
    }

    public async Task<EasySetupResult> SetupGeminiAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.Gemini.ShowCursorSource = true;
        settings.Gemini.ShowDetails = true;
        settings.Gemini.ShowProLimits = true;

        var tokens = _antigravityTokenReader();
        var hasToken = !string.IsNullOrWhiteSpace(tokens.AccessToken)
                       || !string.IsNullOrWhiteSpace(tokens.RefreshToken);

        if (!hasToken)
        {
            _launcher.OpenAntigravity();
            const string message = "Sign in to Antigravity on this machine";
            settings.Gemini.ProLastConnectionStatus = message;
            return new EasySetupResult(message, OpenedExternalUrl: true);
        }

        var status = await _antigravity.TestConnectionAsync(cancellationToken);
        settings.Gemini.ProLastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public Task<EasySetupResult> SetupOpenRouterAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenRouter.ShowProLimits = true;
        settings.OpenRouter.ShowDetails = true;

        var apiKey = CredentialStore.Retrieve(settings.OpenRouter.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _launcher.OpenOpenRouter();
            const string message = "Paste your OpenRouter API key (sk-or-...) in settings";
            settings.OpenRouter.LastConnectionStatus = message;
            return Task.FromResult(new EasySetupResult(message, OpenedExternalUrl: true));
        }

        return TestOpenRouterAsync(settings, cancellationToken);
    }

    public Task<EasySetupResult> SetupOpenCodeAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        settings.OpenCode.ShowDirectSource = true;
        settings.OpenCode.ShowProLimits = true;
        settings.OpenCode.ShowDetails = true;

        var session = CredentialStore.Retrieve(settings.OpenCode.ProSessionCredentialId);
        var hasSession = !string.IsNullOrWhiteSpace(session);
        var hasWorkspace = !string.IsNullOrWhiteSpace(settings.OpenCode.WorkspaceId);

        if (!hasSession || !hasWorkspace)
        {
            _launcher.OpenOpenCode();
            const string message =
                "Sign in at opencode.ai, copy auth cookie + workspace ID from URL";
            settings.OpenCode.ProLastConnectionStatus = message;
            return Task.FromResult(new EasySetupResult(message, OpenedExternalUrl: true));
        }

        return TestOpenCodeAsync(settings, cancellationToken);
    }

    private async Task<EasySetupResult> TestOpenRouterAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken)
    {
        using var client = new OpenRouterUsageClient();
        var status = await client.TestConnectionAsync(
            CredentialStore.Retrieve(settings.OpenRouter.CredentialId) ?? "",
            cancellationToken);
        settings.OpenRouter.LastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    private async Task<EasySetupResult> TestOpenCodeAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken)
    {
        using var client = new OpenCodeUsageClient();
        var session = CredentialStore.Retrieve(settings.OpenCode.ProSessionCredentialId) ?? "";
        var status = await client.TestConnectionAsync(
            session,
            settings.OpenCode.WorkspaceId,
            cancellationToken);
        settings.OpenCode.ProLastConnectionStatus = status;
        return new EasySetupResult(status);
    }

    public EasySetupResult SetupDisk(WidgetSettings settings)
    {
        settings.ShowDiskDrives = true;
        settings.ShowDiskDetails = true;
        return new EasySetupResult("Disk drives enabled");
    }
}

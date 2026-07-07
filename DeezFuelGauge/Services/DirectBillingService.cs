using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class DirectBillingService : IDisposable
{
    private readonly OpenAiBillingClient _openAi;
    private readonly CodexUsageClient _codex;
    private readonly AnthropicBillingClient _anthropic;
    private readonly ClaudeProUsageClient _claudePro;
    private readonly AntigravityUsageClient _antigravity;
    private readonly OpenRouterUsageClient _openRouter;
    private readonly OpenCodeUsageClient _openCode;
    private readonly bool _ownsClients;

    public DirectBillingService(
        OpenAiBillingClient? openAi = null,
        CodexUsageClient? codex = null,
        AnthropicBillingClient? anthropic = null,
        ClaudeProUsageClient? claudePro = null,
        AntigravityUsageClient? antigravity = null,
        OpenRouterUsageClient? openRouter = null,
        OpenCodeUsageClient? openCode = null)
    {
        _ownsClients = openAi is null && codex is null && anthropic is null && claudePro is null &&
                       antigravity is null && openRouter is null && openCode is null;
        _openAi = openAi ?? new OpenAiBillingClient();
        _codex = codex ?? new CodexUsageClient();
        _anthropic = anthropic ?? new AnthropicBillingClient();
        _claudePro = claudePro ?? new ClaudeProUsageClient();
        _antigravity = antigravity ?? new AntigravityUsageClient();
        _openRouter = openRouter ?? new OpenRouterUsageClient();
        _openCode = openCode ?? new OpenCodeUsageClient();
    }

    public async Task<UsageSnapshot> EnrichAsync(
        UsageSnapshot snapshot,
        WidgetSettings settings,
        CancellationToken cancellationToken = default)
    {
        var openAiDirect = settings.OpenAi.ShowDirectSource
            ? await _openAi.FetchAsync(
                settings.OpenAi,
                snapshot.BillingCycleStartMs,
                snapshot.BillingCycleEndMs,
                cancellationToken)
            : DirectProviderSnapshot.Unavailable();

        var codex = settings.OpenAi.ShowProLimits
            ? await _codex.FetchAsync(settings.OpenAi, cancellationToken)
            : CodexSnapshot.Unavailable();

        var claudeDirect = settings.Claude.ShowApiConsoleBilling
            ? await _anthropic.FetchAsync(
                settings.Claude,
                snapshot.BillingCycleStartMs,
                snapshot.BillingCycleEndMs,
                cancellationToken)
            : DirectProviderSnapshot.Unavailable();

        var claudePro = settings.Claude.ShowProLimits
            ? await _claudePro.FetchAsync(settings.Claude, cancellationToken)
            : ClaudeProSnapshot.Unavailable();

        var antigravity = settings.Gemini.ShowProLimits
            ? await _antigravity.FetchAsync(settings.Gemini, cancellationToken)
            : AntigravitySnapshot.Unavailable();

        var openRouter = ProviderFeatureFlags.OpenRouterEnabled && settings.OpenRouter.ShowProLimits
            ? await _openRouter.FetchAsync(settings.OpenRouter, cancellationToken)
            : OpenRouterSnapshot.Unavailable();

        var openCode = settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits
            ? await _openCode.FetchAsync(settings.OpenCode, cancellationToken)
            : OpenCodeSnapshot.Unavailable();

        return CopyWithEnrichment(snapshot, openAiDirect, codex, claudeDirect, claudePro, antigravity, openRouter, openCode);
    }

    internal static UsageSnapshot CopyWithEnrichment(
        UsageSnapshot source,
        DirectProviderSnapshot openAiDirect,
        CodexSnapshot codex,
        DirectProviderSnapshot claudeDirect,
        ClaudeProSnapshot claudePro,
        AntigravitySnapshot antigravity,
        OpenRouterSnapshot openRouter,
        OpenCodeSnapshot openCode) =>
        new()
        {
            PercentUsed = source.PercentUsed,
            RemainingLabel = source.RemainingLabel,
            AutoPercentUsed = source.AutoPercentUsed,
            ApiPercentUsed = source.ApiPercentUsed,
            PlanLimitCents = source.PlanLimitCents,
            BillingCycleStartMs = source.BillingCycleStartMs,
            BillingCycleEndMs = source.BillingCycleEndMs,
            OpenAi = source.OpenAi,
            Claude = source.Claude,
            Gemini = source.Gemini,
            Codex = codex,
            ClaudePro = claudePro,
            OpenAiDirect = openAiDirect,
            ClaudeDirect = claudeDirect,
            Antigravity = antigravity,
            OpenRouter = openRouter,
            OpenCode = openCode,
            IsError = source.IsError,
            ErrorMessage = source.ErrorMessage
        };

    public void Dispose()
    {
        if (!_ownsClients)
            return;

        _openAi.Dispose();
        _codex.Dispose();
        _anthropic.Dispose();
        _claudePro.Dispose();
        _antigravity.Dispose();
        _openRouter.Dispose();
        _openCode.Dispose();
    }
}

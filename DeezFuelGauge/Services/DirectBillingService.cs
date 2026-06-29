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
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        var token = timeoutCts.Token;
        var billingStart = snapshot.BillingCycleStartMs;
        var billingEnd = snapshot.BillingCycleEndMs;

        var openAiDirectTask = settings.OpenAi.ShowDirectSource
            ? _openAi.FetchAsync(settings.OpenAi, billingStart, billingEnd, token)
            : Task.FromResult(DirectProviderSnapshot.Unavailable());

        var codexTask = settings.OpenAi.ShowProLimits
            ? _codex.FetchAsync(settings.OpenAi, token)
            : Task.FromResult(CodexSnapshot.Unavailable());

        var claudeProTask = settings.Claude.ShowProLimits
            ? _claudePro.FetchAsync(settings.Claude, token)
            : Task.FromResult(ClaudeProSnapshot.Unavailable());

        var claudeDirectTask = settings.Claude.ShowApiConsoleBilling
            ? _anthropic.FetchAsync(settings.Claude, billingStart, billingEnd, token)
            : Task.FromResult(DirectProviderSnapshot.Unavailable());

        var antigravityTask = settings.Gemini.ShowProLimits
            ? _antigravity.FetchAsync(settings.Gemini, token)
            : Task.FromResult(AntigravitySnapshot.Unavailable());

        var openRouterTask = settings.OpenRouter.ShowProLimits
            ? _openRouter.FetchAsync(settings.OpenRouter, token)
            : Task.FromResult(OpenRouterSnapshot.Unavailable());

        var openCodeTask = settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits
            ? _openCode.FetchAsync(settings.OpenCode, token)
            : Task.FromResult(OpenCodeSnapshot.Unavailable());

        await Task.WhenAll(
            openAiDirectTask,
            codexTask,
            claudeProTask,
            claudeDirectTask,
            antigravityTask,
            openRouterTask,
            openCodeTask);

        return CopyWithEnrichment(
            snapshot,
            await openAiDirectTask,
            await codexTask,
            await claudeProTask,
            await claudeDirectTask,
            await antigravityTask,
            await openRouterTask,
            await openCodeTask);
    }

    internal static UsageSnapshot CopyWithEnrichment(
        UsageSnapshot source,
        DirectProviderSnapshot openAiDirect,
        CodexSnapshot codex,
        ClaudeProSnapshot claudePro,
        DirectProviderSnapshot claudeDirect,
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
            ClaudePro = claudePro,
            Codex = codex,
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

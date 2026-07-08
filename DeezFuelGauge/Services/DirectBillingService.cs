using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class DirectBillingService : IDisposable
{
    internal static readonly TimeSpan ProviderTimeout = TimeSpan.FromSeconds(30);

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
        var openAiDirectTask = settings.OpenAi.ShowDirectSource
            ? FetchWithTimeout(
                ct => _openAi.FetchAsync(
                    settings.OpenAi,
                    snapshot.BillingCycleStartMs,
                    snapshot.BillingCycleEndMs,
                    ct),
                DirectProviderSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(DirectProviderSnapshot.Unavailable());

        var codexTask = settings.OpenAi.ShowProLimits
            ? FetchWithTimeout(
                ct => _codex.FetchAsync(settings.OpenAi, ct),
                CodexSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(CodexSnapshot.Unavailable());

        var claudeDirectTask = settings.Claude.ShowApiConsoleBilling
            ? FetchWithTimeout(
                ct => _anthropic.FetchAsync(
                    settings.Claude,
                    snapshot.BillingCycleStartMs,
                    snapshot.BillingCycleEndMs,
                    ct),
                DirectProviderSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(DirectProviderSnapshot.Unavailable());

        var claudeProTask = settings.Claude.ShowProLimits
            ? FetchWithTimeout(
                ct => _claudePro.FetchAsync(settings.Claude, ct),
                ClaudeProSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(ClaudeProSnapshot.Unavailable());

        var antigravityTask = settings.Gemini.ShowProLimits
            ? FetchWithTimeout(
                ct => _antigravity.FetchAsync(settings.Gemini, ct),
                AntigravitySnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(AntigravitySnapshot.Unavailable());

        var openRouterTask = ProviderFeatureFlags.OpenRouterEnabled && settings.OpenRouter.ShowProLimits
            ? FetchWithTimeout(
                ct => _openRouter.FetchAsync(settings.OpenRouter, ct),
                OpenRouterSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(OpenRouterSnapshot.Unavailable());

        var openCodeTask = settings.OpenCode.ShowDirectSource || settings.OpenCode.ShowProLimits
            ? FetchWithTimeout(
                ct => _openCode.FetchAsync(settings.OpenCode, ct),
                OpenCodeSnapshot.Unavailable(),
                cancellationToken)
            : Task.FromResult(OpenCodeSnapshot.Unavailable());

        await Task.WhenAll(
            openAiDirectTask,
            codexTask,
            claudeDirectTask,
            claudeProTask,
            antigravityTask,
            openRouterTask,
            openCodeTask);

        return CopyWithEnrichment(
            snapshot,
            await openAiDirectTask,
            await codexTask,
            await claudeDirectTask,
            await claudeProTask,
            await antigravityTask,
            await openRouterTask,
            await openCodeTask);
    }

    private static async Task<T> FetchWithTimeout<T>(
        Func<CancellationToken, Task<T>> fetch,
        T fallback,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProviderTimeout);

        try
        {
            return await fetch(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return fallback;
        }
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

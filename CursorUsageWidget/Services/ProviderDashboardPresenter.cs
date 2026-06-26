using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public static class ProviderDashboardPresenter
{
    public static bool IsCursorDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowCursorSource;

    public static bool IsOpenAiDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowCursorSource || settings.ShowDirectSource || settings.ShowProLimits;

    public static bool IsClaudeDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowCursorSource || settings.ShowProLimits || settings.ShowApiConsoleBilling;

    public static bool IsGeminiDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowCursorSource || settings.ShowProLimits;

    public static bool IsOpenRouterDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowProLimits;

    public static bool IsOpenCodeDashboardVisible(ProviderBillingSettings settings) =>
        settings.ShowDirectSource || settings.ShowProLimits;

    public static double ComputeCursorHeadline(UsageSnapshot snapshot, WidgetSettings settings)
    {
        var values = new List<double> { snapshot.PercentUsed };

        if (settings.ShowBreakdown && snapshot.HasBreakdown)
        {
            if (snapshot.AutoPercentUsed is { } auto)
                values.Add(auto);
            if (snapshot.ApiPercentUsed is { } api)
                values.Add(api);
        }

        return values.Max();
    }

    public static double ComputeOpenAiHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        var values = new List<double>();

        if (settings.ShowCursorSource && snapshot.OpenAi.IsAvailable)
            values.Add(snapshot.OpenAi.PercentUsed);

        if (settings.ShowDirectSource && snapshot.OpenAiDirect.IsAvailable)
            values.Add(snapshot.OpenAiDirect.PercentUsed);

        if (settings.ShowProLimits && snapshot.Codex.IsAvailable)
            values.Add(ProviderLimitsPresenter.HeadlinePercent(snapshot.Codex.SessionPercentUsed, snapshot.Codex.WeeklyPercentUsed));

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeClaudeHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        var values = new List<double>();

        if (settings.ShowCursorSource && snapshot.Claude.IsAvailable)
            values.Add(snapshot.Claude.PercentUsed);

        if (settings.ShowProLimits && snapshot.ClaudePro.IsAvailable)
            values.Add(ProviderLimitsPresenter.HeadlinePercent(snapshot.ClaudePro.SessionPercentUsed, snapshot.ClaudePro.WeeklyPercentUsed));

        if (settings.ShowApiConsoleBilling && snapshot.ClaudeDirect.IsAvailable)
            values.Add(snapshot.ClaudeDirect.PercentUsed);

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeGeminiHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        var values = new List<double>();

        if (settings.ShowCursorSource && snapshot.Gemini.IsAvailable)
            values.Add(snapshot.Gemini.PercentUsed);

        if (settings.ShowProLimits && snapshot.Antigravity.IsAvailable)
            values.Add(ProviderLimitsPresenter.AntigravityHeadlinePercent(snapshot.Antigravity));

        return values.Count > 0 ? values.Max() : 0;
    }

    public static double ComputeOpenRouterHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        if (!settings.ShowProLimits || !snapshot.OpenRouter.IsAvailable)
            return 0;

        return snapshot.OpenRouter.HeadlinePercentUsed;
    }

    public static double ComputeOpenCodeHeadline(UsageSnapshot snapshot, ProviderBillingSettings settings)
    {
        var values = new List<double>();

        if (settings.ShowDirectSource && snapshot.OpenCode.ZenIsAvailable)
        {
            if (snapshot.OpenCode.ZenMonthlyPercentUsed is { } zenMonthly)
                values.Add(zenMonthly);
            else if (snapshot.OpenCode.ZenBalanceUsd is { } balance)
            {
                if (balance <= 1)
                    values.Add(95);
                else if (balance <= 5)
                    values.Add(75);
                else if (balance <= 10)
                    values.Add(50);
            }
        }

        if (settings.ShowProLimits && snapshot.OpenCode.HasGoSubscription)
        {
            values.Add(ProviderLimitsPresenter.HeadlinePercent3(
                snapshot.OpenCode.GoRolling.PercentUsed,
                snapshot.OpenCode.GoWeekly.PercentUsed,
                snapshot.OpenCode.GoMonthly.PercentUsed));
        }

        return values.Count > 0 ? values.Max() : 0;
    }
}

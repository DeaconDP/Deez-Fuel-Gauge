using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class OpenRouterSnapshot
{
    public double? BalanceUsd { get; init; }
    public double? KeyLimitUsd { get; init; }
    public double? KeyLimitRemainingUsd { get; init; }
    public double? KeyLimitPercentUsed { get; init; }
    public string? KeyLimitReset { get; init; }
    public bool IsFreeTier { get; init; }
    public double AllTimeUsageUsd { get; init; }
    public double DailySpendUsd { get; init; }
    public double WeeklySpendUsd { get; init; }
    public double MonthlySpendUsd { get; init; }
    public double? ByokDailySpendUsd { get; init; }
    public bool IncludeByokInLimit { get; init; }
    public double HeadlinePercentUsed { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static OpenRouterSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static OpenRouterSnapshot FromResponses(
        double? balanceUsd,
        KeyBillingData key,
        CreditsResponseData? credits)
    {
        double? balance = balanceUsd ?? credits?.BalanceUsd;
        double? keyLimitPercent = key.LimitUsd is > 0 and var limit
            ? Math.Clamp((limit - (key.LimitRemainingUsd ?? 0)) * 100.0 / limit, 0, 100)
            : null;

        return new OpenRouterSnapshot
        {
            IsAvailable = true,
            BalanceUsd = balance,
            KeyLimitUsd = key.LimitUsd,
            KeyLimitRemainingUsd = key.LimitRemainingUsd,
            KeyLimitPercentUsed = keyLimitPercent,
            KeyLimitReset = key.LimitReset,
            IsFreeTier = key.IsFreeTier,
            AllTimeUsageUsd = key.AllTimeUsageUsd,
            DailySpendUsd = key.DailySpendUsd,
            WeeklySpendUsd = key.WeeklySpendUsd,
            MonthlySpendUsd = key.MonthlySpendUsd,
            ByokDailySpendUsd = key.IncludeByokInLimit ? key.ByokDailySpendUsd : null,
            IncludeByokInLimit = key.IncludeByokInLimit,
            HeadlinePercentUsed = ComputeHeadlinePercent(keyLimitPercent, credits),
            DetailLabel = BuildDetailLabel(balance, key, credits)
        };
    }

    private static string BuildDetailLabel(double? balanceUsd, KeyBillingData key, CreditsResponseData? credits)
    {
        var parts = new List<string>();

        if (key.IsFreeTier)
        {
            parts.Add("free tier");
            parts.Add("daily request limits not reported by API — see openrouter.ai/activity");
        }

        if (key.LimitUsd is { } kl)
        {
            var remaining = key.LimitRemainingUsd ?? 0;
            var limitPart = $"key ${remaining.ToString("F2", CultureInfo.InvariantCulture)} / ${kl.ToString("F2", CultureInfo.InvariantCulture)}";
            if (!string.IsNullOrWhiteSpace(key.LimitReset))
                limitPart += $" ({key.LimitReset})";
            parts.Add(limitPart);
        }
        else if (credits is { TotalCredits: > 0 } c)
        {
            parts.Add($"${c.TotalUsage.ToString("F2", CultureInfo.InvariantCulture)} used of ${c.TotalCredits.ToString("F2", CultureInfo.InvariantCulture)} credits");
        }
        else if (balanceUsd is { } b)
        {
            parts.Add($"${b.ToString("F2", CultureInfo.InvariantCulture)} balance");
        }

        if (key.AllTimeUsageUsd > 0)
            parts.Add($"${key.AllTimeUsageUsd.ToString("F2", CultureInfo.InvariantCulture)} all-time");

        if (key.DailySpendUsd > 0 || key.WeeklySpendUsd > 0 || key.MonthlySpendUsd > 0)
            parts.Add($"${key.DailySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} day · ${key.WeeklySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} wk · ${key.MonthlySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} mo");

        if (key.IncludeByokInLimit && key.ByokDailySpendUsd > 0)
            parts.Add($"BYOK ${key.ByokDailySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} day");

        return parts.Count > 0 ? string.Join(" · ", parts) : "Connected";
    }

    internal static double ComputeHeadlinePercent(double? keyLimitPercent, CreditsResponseData? credits)
    {
        if (keyLimitPercent is { } kp)
            return kp;

        if (credits is { TotalCredits: > 0 } c)
            return Math.Clamp(c.TotalUsage * 100.0 / c.TotalCredits, 0, 100);

        return 0;
    }

    public readonly record struct KeyBillingData(
        double? LimitUsd,
        double? LimitRemainingUsd,
        string? LimitReset,
        bool IsFreeTier,
        double AllTimeUsageUsd,
        double DailySpendUsd,
        double WeeklySpendUsd,
        double MonthlySpendUsd,
        bool IncludeByokInLimit,
        double ByokDailySpendUsd);

    public readonly record struct CreditsResponseData(double BalanceUsd, double TotalCredits, double TotalUsage);
}

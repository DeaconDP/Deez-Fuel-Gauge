using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class CodexSnapshot
{
    public double SessionPercentRemaining { get; init; }
    public double WeeklyPercentRemaining { get; init; }
    public double SessionPercentUsed => Math.Clamp(100 - SessionPercentRemaining, 0, 100);
    public double WeeklyPercentUsed => Math.Clamp(100 - WeeklyPercentRemaining, 0, 100);
    public DateTimeOffset? SessionResetsAt { get; init; }
    public DateTimeOffset? WeeklyResetsAt { get; init; }
    public string? PlanLabel { get; init; }
    public decimal? CreditsBalanceUsd { get; init; }
    public bool LimitReached { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static CodexSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static CodexSnapshot FromUsage(
        string? planType,
        double? sessionUsedPercent,
        double? weeklyUsedPercent,
        DateTimeOffset? sessionResetsAt,
        DateTimeOffset? weeklyResetsAt,
        decimal? creditsBalanceUsd,
        bool limitReached)
    {
        var sessionRemaining = sessionUsedPercent is { } sessionUsed
            ? Math.Clamp(100 - sessionUsed, 0, 100)
            : 100;
        var weeklyRemaining = weeklyUsedPercent is { } weeklyUsed
            ? Math.Clamp(100 - weeklyUsed, 0, 100)
            : 100;
        var planLabel = FormatPlanLabel(planType);
        var creditsText = creditsBalanceUsd is { } balance
            ? $"${balance.ToString("F0", CultureInfo.InvariantCulture)} credits"
            : null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(planLabel))
            parts.Add(planLabel);
        if (creditsText is not null)
            parts.Add(creditsText);
        if (sessionUsedPercent is { } sessionPct)
            parts.Add($"5h {Math.Clamp(sessionPct, 0, 100).ToString("F0", CultureInfo.InvariantCulture)}%");
        if (weeklyUsedPercent is { } weeklyPct)
            parts.Add($"wk {Math.Clamp(weeklyPct, 0, 100).ToString("F0", CultureInfo.InvariantCulture)}%");

        return new CodexSnapshot
        {
            IsAvailable = true,
            PlanLabel = planLabel,
            SessionPercentRemaining = sessionRemaining,
            WeeklyPercentRemaining = weeklyRemaining,
            SessionResetsAt = sessionResetsAt,
            WeeklyResetsAt = weeklyResetsAt,
            CreditsBalanceUsd = creditsBalanceUsd,
            LimitReached = limitReached,
            DetailLabel = parts.Count > 0 ? string.Join(" · ", parts) : "—"
        };
    }

    private static string? FormatPlanLabel(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
            return null;

        if (planType.Contains('_', StringComparison.Ordinal))
        {
            return string.Join(' ', planType.Split('_', StringSplitOptions.RemoveEmptyEntries)
                .Select(static part => part.Length == 0
                    ? part
                    : char.ToUpperInvariant(part[0]) + part[1..].ToLowerInvariant()));
        }

        return planType.Length == 1
            ? planType.ToUpperInvariant()
            : char.ToUpperInvariant(planType[0]) + planType[1..].ToLowerInvariant();
    }
}

using System.Globalization;

namespace CursorUsageWidget.Models;

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
        double sessionUsedPercent,
        double weeklyUsedPercent,
        DateTimeOffset? sessionResetsAt,
        DateTimeOffset? weeklyResetsAt,
        decimal? creditsBalanceUsd,
        bool limitReached)
    {
        var sessionRemaining = Math.Clamp(100 - sessionUsedPercent, 0, 100);
        var weeklyRemaining = Math.Clamp(100 - weeklyUsedPercent, 0, 100);
        var planLabel = FormatPlanLabel(planType);
        var creditsText = creditsBalanceUsd is { } balance
            ? $"${balance.ToString("F0", CultureInfo.InvariantCulture)} credits"
            : null;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(planLabel))
            parts.Add(planLabel);
        if (creditsText is not null)
            parts.Add(creditsText);
        var sessionUsed = Math.Clamp(sessionUsedPercent, 0, 100);
        var weeklyUsed = Math.Clamp(weeklyUsedPercent, 0, 100);
        parts.Add($"5h {sessionUsed.ToString("F0", CultureInfo.InvariantCulture)}%");
        parts.Add($"wk {weeklyUsed.ToString("F0", CultureInfo.InvariantCulture)}%");

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
            DetailLabel = string.Join(" · ", parts)
        };
    }

    private static string? FormatPlanLabel(string? planType)
    {
        if (string.IsNullOrWhiteSpace(planType))
            return null;

        return planType.Length == 1
            ? planType.ToUpperInvariant()
            : char.ToUpperInvariant(planType[0]) + planType[1..].ToLowerInvariant();
    }
}

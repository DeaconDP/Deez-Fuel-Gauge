using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class GrokBotSnapshot
{
    public double PercentUsed { get; init; }
    public DateTimeOffset? PeriodStart { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public bool HasAvailableUsage { get; init; }
    public bool HasNonZeroIncludedLimit { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static GrokBotSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static GrokBotSnapshot FromUsage(
        double usagePercent,
        DateTimeOffset? periodStart,
        DateTimeOffset? resetsAt,
        bool hasAvailableUsage,
        bool hasNonZeroIncludedLimit)
    {
        var percent = Math.Clamp(usagePercent, 0, 100);
        var parts = new List<string>
        {
            $"wk {percent.ToString("F0", CultureInfo.InvariantCulture)}%"
        };

        if (resetsAt is { } reset)
            parts.Add($"resets {FormatResetHint(reset)}");

        if (!hasNonZeroIncludedLimit && hasAvailableUsage)
            parts.Add("on-demand");

        return new GrokBotSnapshot
        {
            IsAvailable = true,
            PercentUsed = percent,
            PeriodStart = periodStart,
            ResetsAt = resetsAt,
            HasAvailableUsage = hasAvailableUsage,
            HasNonZeroIncludedLimit = hasNonZeroIncludedLimit,
            DetailLabel = string.Join(" · ", parts)
        };
    }

    private static string FormatResetHint(DateTimeOffset resetsAt)
    {
        var local = resetsAt.ToLocalTime();
        var remaining = local - DateTimeOffset.Now;
        if (remaining <= TimeSpan.Zero)
            return "soon";

        if (remaining.TotalHours < 48)
            return local.ToString("ddd HH:mm", CultureInfo.CurrentCulture);

        return local.ToString("ddd d MMM", CultureInfo.CurrentCulture);
    }
}

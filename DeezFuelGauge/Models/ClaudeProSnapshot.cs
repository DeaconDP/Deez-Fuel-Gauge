using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class ClaudeProSnapshot
{
    public double SessionPercentUsed { get; init; }
    public double WeeklyPercentUsed { get; init; }
    public DateTimeOffset? SessionResetsAt { get; init; }
    public DateTimeOffset? WeeklyResetsAt { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static ClaudeProSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static ClaudeProSnapshot FromUsage(
        double sessionPercent,
        double weeklyPercent,
        DateTimeOffset? sessionResetsAt,
        DateTimeOffset? weeklyResetsAt)
    {
        var session = Math.Clamp(sessionPercent, 0, 100);
        var weekly = Math.Clamp(weeklyPercent, 0, 100);
        var detail = $"5h {session.ToString("F0", CultureInfo.InvariantCulture)}% · wk {weekly.ToString("F0", CultureInfo.InvariantCulture)}%";

        return new ClaudeProSnapshot
        {
            IsAvailable = true,
            SessionPercentUsed = session,
            WeeklyPercentUsed = weekly,
            SessionResetsAt = sessionResetsAt,
            WeeklyResetsAt = weeklyResetsAt,
            DetailLabel = detail
        };
    }
}

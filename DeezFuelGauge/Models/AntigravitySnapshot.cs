using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class AntigravityGroupSnapshot
{
    public double SessionPercentRemaining { get; init; }
    public double WeeklyPercentRemaining { get; init; }
    public double SessionPercentUsed => Math.Clamp(100 - SessionPercentRemaining, 0, 100);
    public double WeeklyPercentUsed => Math.Clamp(100 - WeeklyPercentRemaining, 0, 100);
    public DateTimeOffset? SessionResetsAt { get; init; }
    public DateTimeOffset? WeeklyResetsAt { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static AntigravityGroupSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static AntigravityGroupSnapshot FromUsage(
        double sessionPercentRemaining,
        double weeklyPercentRemaining,
        DateTimeOffset? sessionResetsAt,
        DateTimeOffset? weeklyResetsAt)
    {
        var session = Math.Clamp(sessionPercentRemaining, 0, 100);
        var weekly = Math.Clamp(weeklyPercentRemaining, 0, 100);
        var sessionUsed = Math.Clamp(100 - session, 0, 100);
        var weeklyUsed = Math.Clamp(100 - weekly, 0, 100);
        var detail = $"5h {sessionUsed.ToString("F0", CultureInfo.InvariantCulture)}% · wk {weeklyUsed.ToString("F0", CultureInfo.InvariantCulture)}%";

        return new AntigravityGroupSnapshot
        {
            IsAvailable = true,
            SessionPercentRemaining = session,
            WeeklyPercentRemaining = weekly,
            SessionResetsAt = sessionResetsAt,
            WeeklyResetsAt = weeklyResetsAt,
            DetailLabel = detail
        };
    }
}

public sealed class AntigravitySnapshot
{
    public AntigravityGroupSnapshot Gemini { get; init; } = AntigravityGroupSnapshot.Unavailable();
    public AntigravityGroupSnapshot ThirdParty { get; init; } = AntigravityGroupSnapshot.Unavailable();
    public string? PlanLabel { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static AntigravitySnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static AntigravitySnapshot FromGroups(
        string? planLabel,
        AntigravityGroupSnapshot gemini,
        AntigravityGroupSnapshot thirdParty)
    {
        var isAvailable = gemini.IsAvailable || thirdParty.IsAvailable;
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(planLabel))
            parts.Add(planLabel);
        if (gemini.IsAvailable)
            parts.Add($"Gemini {gemini.DetailLabel}");
        if (thirdParty.IsAvailable)
            parts.Add($"3P {thirdParty.DetailLabel}");

        return new AntigravitySnapshot
        {
            IsAvailable = isAvailable,
            PlanLabel = planLabel,
            Gemini = gemini,
            ThirdParty = thirdParty,
            DetailLabel = parts.Count > 0 ? string.Join(" · ", parts) : "—"
        };
    }
}

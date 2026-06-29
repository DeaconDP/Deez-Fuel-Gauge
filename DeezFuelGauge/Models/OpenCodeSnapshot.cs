using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class OpenCodeWindowSnapshot
{
    public double PercentUsed { get; init; }
    public DateTimeOffset? ResetsAt { get; init; }
    public bool IsAvailable { get; init; }

    public static OpenCodeWindowSnapshot Unavailable() => new() { IsAvailable = false };

    public static OpenCodeWindowSnapshot FromUsage(double percentUsed, DateTimeOffset? resetsAt) => new()
    {
        IsAvailable = true,
        PercentUsed = Math.Clamp(percentUsed, 0, 100),
        ResetsAt = resetsAt
    };
}

public sealed class OpenCodeSnapshot
{
    public decimal? ZenBalanceUsd { get; init; }
    public decimal? ZenMonthlyCapUsd { get; init; }
    public decimal? ZenMonthlyUsedUsd { get; init; }
    public double? ZenMonthlyPercentUsed { get; init; }
    public OpenCodeWindowSnapshot GoRolling { get; init; } = OpenCodeWindowSnapshot.Unavailable();
    public OpenCodeWindowSnapshot GoWeekly { get; init; } = OpenCodeWindowSnapshot.Unavailable();
    public OpenCodeWindowSnapshot GoMonthly { get; init; } = OpenCodeWindowSnapshot.Unavailable();
    public bool HasGoSubscription { get; init; }
    public bool ZenIsAvailable { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static OpenCodeSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static OpenCodeSnapshot FromData(
        decimal? zenBalanceUsd,
        decimal? zenMonthlyCapUsd,
        decimal? zenMonthlyUsedUsd,
        OpenCodeWindowSnapshot? goRolling,
        OpenCodeWindowSnapshot? goWeekly,
        OpenCodeWindowSnapshot? goMonthly,
        bool hasGoSubscription)
    {
        var rolling = goRolling ?? OpenCodeWindowSnapshot.Unavailable();
        var weekly = goWeekly ?? OpenCodeWindowSnapshot.Unavailable();
        var monthly = goMonthly ?? OpenCodeWindowSnapshot.Unavailable();
        var zenAvailable = zenBalanceUsd is not null || zenMonthlyCapUsd is not null;
        var goAvailable = hasGoSubscription && (rolling.IsAvailable || weekly.IsAvailable || monthly.IsAvailable);

        double? zenMonthlyPercent = zenMonthlyCapUsd is > 0 and var cap && zenMonthlyUsedUsd is { } used
            ? Math.Clamp((double)used * 100.0 / (double)cap, 0, 100)
            : null;

        var parts = new List<string>();
        if (zenBalanceUsd is { } balance)
            parts.Add($"Zen ${balance.ToString("F2", CultureInfo.InvariantCulture)}");
        if (zenMonthlyPercent is { } zmp)
            parts.Add($"mo {zmp.ToString("F0", CultureInfo.InvariantCulture)}%");
        if (goAvailable)
        {
            if (rolling.IsAvailable)
                parts.Add($"5h {rolling.PercentUsed.ToString("F0", CultureInfo.InvariantCulture)}%");
            if (weekly.IsAvailable)
                parts.Add($"wk {weekly.PercentUsed.ToString("F0", CultureInfo.InvariantCulture)}%");
            if (monthly.IsAvailable)
                parts.Add($"mo {monthly.PercentUsed.ToString("F0", CultureInfo.InvariantCulture)}%");
        }

        return new OpenCodeSnapshot
        {
            IsAvailable = zenAvailable || goAvailable,
            ZenIsAvailable = zenAvailable,
            ZenBalanceUsd = zenBalanceUsd,
            ZenMonthlyCapUsd = zenMonthlyCapUsd,
            ZenMonthlyUsedUsd = zenMonthlyUsedUsd,
            ZenMonthlyPercentUsed = zenMonthlyPercent,
            GoRolling = rolling,
            GoWeekly = weekly,
            GoMonthly = monthly,
            HasGoSubscription = hasGoSubscription,
            DetailLabel = parts.Count > 0 ? string.Join(" · ", parts) : "Connected"
        };
    }
}

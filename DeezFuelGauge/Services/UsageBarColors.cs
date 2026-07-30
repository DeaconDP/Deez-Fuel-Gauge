using Avalonia.Media;

namespace DeezFuelGauge.Services;

public static class UsageBarColors
{
    public static readonly TimeSpan FiveHourWindow = TimeSpan.FromHours(5);
    public static readonly TimeSpan WeeklyWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan MonthlyWindow = TimeSpan.FromDays(30);

    /// <summary>Muted steel blue — just after a reset (progress near 0%).</summary>
    public static readonly Color MutedResetBlue = Color.FromRgb(0x6A, 0x84, 0x99);

    /// <summary>Muted sage — mid-window.</summary>
    public static readonly Color MutedResetGreen = Color.FromRgb(0x6A, 0x8F, 0x6E);

    /// <summary>Muted ochre — reset soon (progress near 100%).</summary>
    public static readonly Color MutedResetYellow = Color.FromRgb(0xA8, 0x98, 0x50);

    public static readonly Color ResetLabelFallback = Color.FromRgb(0x77, 0x77, 0x77);

    public static Color GetColorForPercent(double percentUsed)
    {
        if (percentUsed >= 90)
            return Color.FromRgb(0xFF, 0x98, 0x00);
        if (percentUsed >= 75)
            return Color.FromRgb(0xFF, 0xEB, 0x3B);
        if (percentUsed >= 25)
            return Color.FromRgb(0x4C, 0xAF, 0x50);
        return Color.FromRgb(0x4D, 0x9F, 0xFF);
    }

    /// <summary>
    /// Muted blue → green → yellow for reset timer text.
    /// 0% = just after reset; 100% = reset imminent.
    /// </summary>
    public static Color GetMutedColorForResetProgress(double progressPercent)
    {
        if (progressPercent >= 75)
            return MutedResetYellow;
        if (progressPercent >= 25)
            return MutedResetGreen;
        return MutedResetBlue;
    }

    /// <summary>
    /// Elapsed fraction of a fixed-length window ending at <paramref name="resetsAt"/>.
    /// </summary>
    public static double GetResetProgressPercent(
        DateTimeOffset resetsAt,
        TimeSpan windowDuration,
        DateTimeOffset? now = null)
    {
        if (windowDuration <= TimeSpan.Zero)
            return 0;

        var at = now ?? DateTimeOffset.UtcNow;
        var remaining = resetsAt - at;
        if (remaining <= TimeSpan.Zero)
            return 100;

        var elapsed = windowDuration - remaining;
        return Math.Clamp(elapsed.TotalSeconds / windowDuration.TotalSeconds * 100.0, 0, 100);
    }

    /// <summary>
    /// Elapsed fraction between known window start and end.
    /// </summary>
    public static double GetResetProgressPercent(
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        DateTimeOffset? now = null)
    {
        var duration = windowEnd - windowStart;
        return GetResetProgressPercent(windowEnd, duration, now);
    }
}

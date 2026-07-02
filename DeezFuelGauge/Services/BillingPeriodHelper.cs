namespace DeezFuelGauge.Services;

internal static class BillingPeriodHelper
{
    public static (long StartUnix, long EndUnix) CurrentCalendarMonthUtc()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        return (start.ToUnixTimeSeconds(), end.ToUnixTimeSeconds());
    }

    public static (string StartingAt, string EndingAt) CurrentCalendarMonthIso8601()
    {
        var now = DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddMonths(1);
        return (start.ToString("yyyy-MM-ddTHH:mm:ssZ"), end.ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    public static (long StartMs, long EndMs)? FromCursorCycle(long? startMs, long? endMs)
    {
        if (startMs is null or <= 0 || endMs is null or <= 0)
            return null;

        return (startMs.Value, endMs.Value);
    }

    public static DateTimeOffset ResolveCursorCycleEnd(long? billingCycleEndMs, DateTimeOffset? referenceUtc = null) =>
        billingCycleEndMs is > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(billingCycleEndMs.Value)
            : CurrentCalendarMonthEndUtc(referenceUtc);

    public static DateTimeOffset CurrentCalendarMonthEndUtc(DateTimeOffset? referenceUtc = null)
    {
        var now = referenceUtc ?? DateTimeOffset.UtcNow;
        var start = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        return start.AddMonths(1);
    }
}

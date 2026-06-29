namespace DeezFuelGauge.Models;

using System.Globalization;

public sealed class ProviderUsageSnapshot
{
    public double PercentUsed { get; init; }
    public string DetailLabel { get; init; } = "";
    public string? StatusMessage { get; init; }
    public bool IsAvailable { get; init; }

    public static ProviderUsageSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static ProviderUsageSnapshot FromSpend(double spendCents, long planLimitCents)
    {
        if (planLimitCents <= 0)
            return Unavailable();

        var percent = spendCents * 100.0 / planLimitCents;
        return new ProviderUsageSnapshot
        {
            IsAvailable = true,
            PercentUsed = Math.Clamp(percent, 0, 100),
            DetailLabel = $"${(spendCents / 100.0).ToString("F2", CultureInfo.InvariantCulture)} of plan"
        };
    }
}

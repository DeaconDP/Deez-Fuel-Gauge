using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class FalSnapshot
{
    public double? BalanceUsd { get; init; }
    public string Currency { get; init; } = "USD";
    public double HeadlinePercentUsed { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static FalSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static FalSnapshot FromBalance(double balanceUsd, string? currency = null)
    {
        var currencyCode = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant();
        var percent = ComputeHeadlinePercent(balanceUsd);
        var balanceLabel = currencyCode == "USD"
            ? $"${balanceUsd.ToString("F2", CultureInfo.InvariantCulture)} left"
            : $"{balanceUsd.ToString("F2", CultureInfo.InvariantCulture)} {currencyCode} left";

        return new FalSnapshot
        {
            IsAvailable = true,
            BalanceUsd = balanceUsd,
            Currency = currencyCode,
            HeadlinePercentUsed = percent,
            DetailLabel = balanceLabel
        };
    }

    /// <summary>
    /// OpenCode Zen-style low-balance heuristic: empty tank when balance is 0;
    /// otherwise escalate toward full as remaining credits shrink.
    /// </summary>
    internal static double ComputeHeadlinePercent(double balanceUsd)
    {
        if (balanceUsd <= 0)
            return 100;
        if (balanceUsd <= 1)
            return 95;
        if (balanceUsd <= 5)
            return 75;
        if (balanceUsd <= 10)
            return 50;
        return 0;
    }
}

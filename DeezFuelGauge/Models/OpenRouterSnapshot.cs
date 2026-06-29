using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class OpenRouterSnapshot
{
    public double? BalanceUsd { get; init; }
    public double? KeyLimitUsd { get; init; }
    public double? KeyLimitRemainingUsd { get; init; }
    public double? KeyLimitPercentUsed { get; init; }
    public double DailySpendUsd { get; init; }
    public double WeeklySpendUsd { get; init; }
    public double MonthlySpendUsd { get; init; }
    public double HeadlinePercentUsed { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static OpenRouterSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static OpenRouterSnapshot FromResponses(
        double? balanceUsd,
        double? keyLimitUsd,
        double? keyLimitRemainingUsd,
        double dailySpendUsd,
        double weeklySpendUsd,
        double monthlySpendUsd)
    {
        double? keyLimitPercent = keyLimitUsd is > 0 and var limit
            ? Math.Clamp((limit - (keyLimitRemainingUsd ?? 0)) * 100.0 / limit, 0, 100)
            : null;

        return new OpenRouterSnapshot
        {
            IsAvailable = true,
            BalanceUsd = balanceUsd,
            KeyLimitUsd = keyLimitUsd,
            KeyLimitRemainingUsd = keyLimitRemainingUsd,
            KeyLimitPercentUsed = keyLimitPercent,
            DailySpendUsd = dailySpendUsd,
            WeeklySpendUsd = weeklySpendUsd,
            MonthlySpendUsd = monthlySpendUsd,
            HeadlinePercentUsed = ComputeHeadlinePercent(keyLimitPercent, balanceUsd),
            DetailLabel = BuildDetailLabel(balanceUsd, keyLimitUsd, keyLimitRemainingUsd, dailySpendUsd, weeklySpendUsd, monthlySpendUsd)
        };
    }

    private static string BuildDetailLabel(
        double? balanceUsd,
        double? keyLimitUsd,
        double? keyLimitRemainingUsd,
        double dailySpendUsd,
        double weeklySpendUsd,
        double monthlySpendUsd)
    {
        var parts = new List<string>();
        if (balanceUsd is { } b)
            parts.Add($"${b.ToString("F2", CultureInfo.InvariantCulture)} balance");
        if (keyLimitUsd is { } kl)
        {
            var remaining = keyLimitRemainingUsd ?? 0;
            parts.Add($"key ${remaining.ToString("F2", CultureInfo.InvariantCulture)} / ${kl.ToString("F2", CultureInfo.InvariantCulture)}");
        }

        if (dailySpendUsd > 0 || weeklySpendUsd > 0 || monthlySpendUsd > 0)
            parts.Add($"${dailySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} day · ${weeklySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} wk · ${monthlySpendUsd.ToString("F2", CultureInfo.InvariantCulture)} mo");

        return parts.Count > 0 ? string.Join(" · ", parts) : "Connected";
    }

    internal static double ComputeHeadlinePercent(double? keyLimitPercent, double? balanceUsd)
    {
        if (keyLimitPercent is { } kp)
            return kp;

        if (balanceUsd is { } balance)
        {
            if (balance <= 1)
                return 95;
            if (balance <= 5)
                return 75;
            if (balance <= 10)
                return 50;
            return 10;
        }

        return 0;
    }
}

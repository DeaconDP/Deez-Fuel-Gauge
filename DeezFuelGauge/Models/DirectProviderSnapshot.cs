using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class DirectProviderSnapshot
{
    public double SpendUsd { get; init; }
    public double? BudgetUsd { get; init; }
    public double? RemainingUsd { get; init; }
    public double? GrantedUsd { get; init; }
    public double PercentUsed { get; init; }
    public long InputTokens { get; init; }
    public long OutputTokens { get; init; }
    public bool IsAvailable { get; init; }
    public string? StatusMessage { get; init; }
    public string DetailLabel { get; init; } = "";

    public static DirectProviderSnapshot Unavailable(string? message = null) => new()
    {
        IsAvailable = false,
        StatusMessage = message,
        DetailLabel = message ?? "—"
    };

    public static DirectProviderSnapshot FromBilling(
        double spendUsd,
        double? budgetUsd,
        long inputTokens,
        long outputTokens,
        string? statusMessage = null)
    {
        var percent = budgetUsd is > 0
            ? Math.Clamp(spendUsd * 100.0 / budgetUsd.Value, 0, 100)
            : 0;

        var spendLabel = $"${spendUsd.ToString("F2", CultureInfo.InvariantCulture)}";
        var budgetLabel = budgetUsd is > 0
            ? $" / ${budgetUsd.Value.ToString("F2", CultureInfo.InvariantCulture)}"
            : "";
        var tokenLabel = inputTokens + outputTokens > 0
            ? $" · {inputTokens:N0} in / {outputTokens:N0} out tokens"
            : "";

        return new DirectProviderSnapshot
        {
            IsAvailable = true,
            SpendUsd = spendUsd,
            BudgetUsd = budgetUsd,
            PercentUsed = percent,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            StatusMessage = statusMessage,
            DetailLabel = $"{spendLabel}{budgetLabel}{tokenLabel}"
        };
    }

    public static DirectProviderSnapshot FromCreditGrants(
        double grantedUsd,
        double usedUsd,
        double remainingUsd,
        string? statusMessage = null)
    {
        var percent = grantedUsd > 0
            ? Math.Clamp(usedUsd * 100.0 / grantedUsd, 0, 100)
            : remainingUsd <= 0 ? 100 : 0;

        var remainingLabel = $"${remainingUsd.ToString("F2", CultureInfo.InvariantCulture)} remaining";
        var grantedLabel = grantedUsd > 0
            ? $" of ${grantedUsd.ToString("F2", CultureInfo.InvariantCulture)}"
            : "";

        return new DirectProviderSnapshot
        {
            IsAvailable = true,
            SpendUsd = usedUsd,
            GrantedUsd = grantedUsd,
            RemainingUsd = remainingUsd,
            PercentUsed = percent,
            StatusMessage = statusMessage,
            DetailLabel = $"{remainingLabel}{grantedLabel}"
        };
    }
}

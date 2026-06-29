namespace DeezFuelGauge.Models;

public sealed record QuotaAlert(
    string SourceId,
    string ProviderKey,
    string Label,
    double PercentUsed,
    int DaysRemaining,
    string Message);

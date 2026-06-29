using System.Globalization;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

internal static class UsageResponseParser
{
    public static UsageSnapshot? ParseCurrentPeriodUsage(JsonElement root)
    {
        if (!root.TryGetProperty("planUsage", out var planUsage))
            return null;

        if (!planUsage.TryGetProperty("limit", out var limitElement))
            return null;

        var limit = limitElement.GetInt64();
        if (limit <= 0)
            return null;

        var percent = GetFinitePercent(planUsage, "totalPercentUsed");
        if (percent is null)
        {
            var includedSpend = planUsage.TryGetProperty("includedSpend", out var spendEl)
                ? spendEl.GetInt64()
                : 0;
            percent = limit > 0 ? includedSpend * 100.0 / limit : 0;
        }

        var remaining = planUsage.TryGetProperty("remaining", out var remainingEl)
            ? remainingEl.GetInt64()
            : Math.Max(0, limit - (planUsage.TryGetProperty("includedSpend", out var inc) ? inc.GetInt64() : 0));

        var autoPercent = GetFinitePercent(planUsage, "autoPercentUsed");
        var apiPercent = GetFinitePercent(planUsage, "apiPercentUsed");

        long? billingCycleStartMs = null;
        long? billingCycleEndMs = null;
        if (root.TryGetProperty("billingCycleStart", out var startEl)
            && long.TryParse(startEl.GetString(), out var startMs))
            billingCycleStartMs = startMs;
        if (root.TryGetProperty("billingCycleEnd", out var endEl)
            && long.TryParse(endEl.GetString(), out var endMs))
            billingCycleEndMs = endMs;

        return new UsageSnapshot
        {
            PercentUsed = Math.Clamp(percent.Value, 0, 100),
            RemainingLabel = $"${(remaining / 100.0).ToString("F2", CultureInfo.InvariantCulture)} left",
            AutoPercentUsed = autoPercent is not null ? Math.Clamp(autoPercent.Value, 0, 100) : null,
            ApiPercentUsed = apiPercent is not null ? Math.Clamp(apiPercent.Value, 0, 100) : null,
            PlanLimitCents = limit,
            BillingCycleStartMs = billingCycleStartMs,
            BillingCycleEndMs = billingCycleEndMs
        };
    }

    private static double? GetFinitePercent(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
            return null;

        if (element.ValueKind != JsonValueKind.Number)
            return null;

        var value = element.GetDouble();
        return double.IsFinite(value) ? value : null;
    }
}

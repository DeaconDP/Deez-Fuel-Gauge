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

        return new UsageSnapshot
        {
            PercentUsed = Math.Clamp(percent.Value, 0, 100),
            RemainingLabel = $"${(remaining / 100.0).ToString("F2", CultureInfo.InvariantCulture)} left",
            AutoPercentUsed = autoPercent is not null ? Math.Clamp(autoPercent.Value, 0, 100) : null,
            ApiPercentUsed = apiPercent is not null ? Math.Clamp(apiPercent.Value, 0, 100) : null,
            PlanLimitCents = limit,
            BillingCycleStartMs = TryParseTimestampMs(root, "billingCycleStart"),
            BillingCycleEndMs = TryParseTimestampMs(root, "billingCycleEnd")
        };
    }

    /// <summary>
    /// Cursor returns cycle bounds as unix-ms strings, unix-ms numbers, or ISO-8601 strings.
    /// </summary>
    internal static long? TryParseTimestampMs(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
            return null;

        return TryParseTimestampMs(element);
    }

    internal static long? TryParseTimestampMs(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            if (element.TryGetInt64(out var numeric))
                return NormalizeUnixTimestampMs(numeric);

            if (element.TryGetDouble(out var floating) && double.IsFinite(floating) && floating > 0)
                return NormalizeUnixTimestampMs((long)floating);

            return null;
        }

        if (element.ValueKind != JsonValueKind.String)
            return null;

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms))
            return NormalizeUnixTimestampMs(ms);

        if (DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var iso))
            return iso.ToUnixTimeMilliseconds();

        return null;
    }

    private static long NormalizeUnixTimestampMs(long value) =>
        // Values below ~2001-09-09 in ms are almost certainly unix seconds.
        value > 1_000_000_000_000 ? value : value * 1000;

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

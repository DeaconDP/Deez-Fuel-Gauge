using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class QuotaAlertPresenter
{
    public static string FormatHeadlineBadge(bool hasAlert) => hasAlert ? " ⚠" : "";

    public static string FormatHeaderSummary(IReadOnlyList<QuotaAlert> alerts)
    {
        if (alerts.Count == 0)
            return "";

        return alerts.Count == 1
            ? "1 quota expiring soon"
            : $"{alerts.Count} quotas expiring soon";
    }

    public static string FormatHeaderTooltip(IReadOnlyList<QuotaAlert> alerts) =>
        alerts.Count == 0 ? "" : string.Join(Environment.NewLine, alerts.Select(a => a.Message));

    public static string FormatProviderTooltip(IEnumerable<QuotaAlert> alerts)
    {
        var messages = alerts.Select(a => a.Message).ToList();
        return messages.Count == 0 ? "" : string.Join(Environment.NewLine, messages);
    }
}

using System.Globalization;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class CursorBreakdownPresenter
{
    public static string FormatSummary(double autoPercent, double apiPercent)
    {
        var autoRounded = Math.Round(autoPercent);
        var apiRounded = Math.Round(apiPercent);
        return $"{autoRounded.ToString(CultureInfo.InvariantCulture)}% Auto and {apiRounded.ToString(CultureInfo.InvariantCulture)}% API used";
    }

    public static string FormatApiPlanNote(long? planLimitCents)
    {
        const string baseText = "Additional usage beyond limits consumes on-demand spend.";
        if (planLimitCents is not > 0)
            return baseText;

        var dollars = (long)Math.Round(planLimitCents.Value / 100.0, MidpointRounding.AwayFromZero);
        return $"{baseText} Your plan includes at least ${dollars.ToString(CultureInfo.InvariantCulture)} of API usage.";
    }
}

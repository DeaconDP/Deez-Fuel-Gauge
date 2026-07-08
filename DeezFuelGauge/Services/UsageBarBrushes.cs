using Avalonia.Media;

namespace DeezFuelGauge.Services;

public static class UsageBarBrushes
{
    private static readonly Dictionary<Color, SolidColorBrush> Cache = new();

    public static SolidColorBrush GetBrushForPercent(double percentUsed) =>
        GetBrush(UsageBarColors.GetColorForPercent(percentUsed));

    public static SolidColorBrush GetBrush(Color color)
    {
        if (Cache.TryGetValue(color, out var brush))
            return brush;

        brush = new SolidColorBrush(color);
        Cache[color] = brush;
        return brush;
    }
}

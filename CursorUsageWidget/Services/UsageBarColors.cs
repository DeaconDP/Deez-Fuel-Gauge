using Avalonia.Media;

namespace CursorUsageWidget.Services;

public static class UsageBarColors
{
    public static Color GetColorForPercent(double percentUsed)
    {
        if (percentUsed >= 90)
            return Color.FromRgb(0xFF, 0x98, 0x00);
        if (percentUsed >= 75)
            return Color.FromRgb(0xFF, 0xEB, 0x3B);
        if (percentUsed >= 25)
            return Color.FromRgb(0x4C, 0xAF, 0x50);
        return Color.FromRgb(0x4D, 0x9F, 0xFF);
    }
}

namespace CursorUsageWidget.Services;

public static class WindowAnchorHelper
{
    /// <summary>
    /// Returns a new Y position so the window bottom edge stays fixed when height changes.
    /// </summary>
    public static int CompensateVerticalGrowth(double oldHeight, double newHeight, int currentY)
    {
        var delta = (int)Math.Round(newHeight - oldHeight);
        return currentY - delta;
    }
}

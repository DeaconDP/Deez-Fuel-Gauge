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

    /// <summary>
    /// Returns the window Y position that keeps the bottom edge at <paramref name="anchorBottom"/>.
    /// </summary>
    public static int ComputeBottomAnchoredY(double anchorBottom, double height) =>
        (int)Math.Round(anchorBottom - height);

    public static (int X, int Y) ComputeCenteredPosition(
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight,
        int windowWidth,
        int windowHeight)
    {
        var x = workAreaX + Math.Max(0, (workAreaWidth - windowWidth) / 2);
        var y = workAreaY + Math.Max(0, (workAreaHeight - windowHeight) / 2);
        return (x, y);
    }
}

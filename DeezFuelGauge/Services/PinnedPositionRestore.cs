namespace DeezFuelGauge.Services;

/// <summary>
/// Resolves a pinned Left/Top for restore using the window size those coords were saved for
/// (compact pill when compact mode is on, otherwise the full widget).
/// </summary>
public static class PinnedPositionRestore
{
    public static (int X, int Y, bool Moved) Resolve(
        double savedLeft,
        double savedTop,
        double restoreWidth,
        double restoreHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas)
    {
        var width = Math.Max(1, (int)Math.Round(restoreWidth));
        var height = Math.Max(1, (int)Math.Round(restoreHeight));
        var savedX = (int)savedLeft;
        var savedY = (int)savedTop;
        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(
            savedX,
            savedY,
            width,
            height,
            workingAreas);
        return (x, y, x != savedX || y != savedY);
    }
}

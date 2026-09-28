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

    /// <summary>
    /// Windows stable compact keeps a full-size HWND with a BR-aligned pill.
    /// Clamp the saved compact rest, then return the full-host top-left that keeps that pill.
    /// Setting Position to the compact rest itself parks the clipped pill off-screen near edges.
    /// </summary>
    public static (int HostX, int HostY, int RestX, int RestY, bool Moved) ResolveStableCompactHost(
        double savedLeft,
        double savedTop,
        double compactWidth,
        double compactHeight,
        double fullWidth,
        double fullHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas)
    {
        var (restX, restY, moved) = Resolve(
            savedLeft,
            savedTop,
            compactWidth,
            compactHeight,
            workingAreas);
        var (hostX, hostY) = CompactPlacement.TransitionAnimEnd(
            new CompactRestOrigin(restX, restY, compactWidth, compactHeight),
            goingFull: true,
            fullWidth,
            fullHeight,
            settingsAnchorBottom: null);
        return (hostX, hostY, restX, restY, moved);
    }
}

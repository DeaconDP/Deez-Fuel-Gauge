namespace DeezFuelGauge.Services;

public readonly record struct CompactRestOrigin(int X, int Y, double Width, double Height);

public static class CompactPlacement
{
    public static (int X, int Y) TransitionEnd(
        CompactRestOrigin rest,
        bool goingFull,
        double fullWidth,
        double fullHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas,
        double? settingsAnchorBottom)
    {
        if (!goingFull)
            return (rest.X, rest.Y);

        var anchorBottom = settingsAnchorBottom ?? (rest.Y + rest.Height);
        var (x, y) = WindowAnchorHelper.PlaceKeepingBottomRight(
            rest.X + rest.Width,
            anchorBottom,
            fullWidth,
            fullHeight);

        return WindowAnchorHelper.ClampToWorkingAreas(
            x,
            y,
            Math.Max(1, (int)Math.Round(fullWidth)),
            Math.Max(1, (int)Math.Round(fullHeight)),
            workingAreas);
    }

    public static (int X, int Y) CollapseEnd(
        int currentX,
        int currentY,
        double currentWidth,
        double currentHeight,
        double compactWidth,
        double compactHeight) =>
        WindowAnchorHelper.PlaceKeepingBottomRight(
            currentX + currentWidth,
            currentY + currentHeight,
            compactWidth,
            compactHeight);

    public static CompactRestOrigin AfterExpandedDrag(
        CompactRestOrigin rest,
        int placedX,
        int placedY,
        int draggedX,
        int draggedY) =>
        rest with
        {
            X = rest.X + (draggedX - placedX),
            Y = rest.Y + (draggedY - placedY)
        };

    public static bool ShouldApplyLayoutShift(
        bool pending,
        bool transitionOwnsFrame,
        double anchorFromHeight,
        double newHeight)
    {
        if (!pending || transitionOwnsFrame)
            return false;

        return Math.Abs(newHeight - anchorFromHeight) >= 0.5;
    }
}

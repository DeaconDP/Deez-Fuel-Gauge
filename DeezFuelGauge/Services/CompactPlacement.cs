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

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            rest.Width,
            rest.Height,
            fullWidth,
            fullHeight,
            rest.X,
            rest.Y,
            workingAreas);

        if (settingsAnchorBottom is { } bottom)
            y = WindowAnchorHelper.ComputeBottomAnchoredY(bottom, fullHeight);

        return (x, y);
    }

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

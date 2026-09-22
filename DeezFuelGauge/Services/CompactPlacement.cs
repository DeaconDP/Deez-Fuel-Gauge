namespace DeezFuelGauge.Services;

public readonly record struct CompactRestOrigin(int X, int Y, double Width, double Height);

public static class CompactPlacement
{
    /// <summary>
    /// Unclamped expand/collapse target that keeps the compact bottom-right corner fixed.
    /// Use this for scale-host animation so start and end share a corner.
    /// </summary>
    public static (int X, int Y) TransitionAnimEnd(
        CompactRestOrigin rest,
        bool goingFull,
        double fullWidth,
        double fullHeight,
        double? settingsAnchorBottom)
    {
        if (!goingFull)
            return (rest.X, rest.Y);

        var anchorBottom = settingsAnchorBottom ?? (rest.Y + rest.Height);
        return WindowAnchorHelper.PlaceKeepingBottomRight(
            rest.X + rest.Width,
            anchorBottom,
            fullWidth,
            fullHeight);
    }

    /// <summary>
    /// Final expand/collapse target. Keeps the compact bottom-right corner fixed and does not
    /// clamp the expanded rect into the work area (partial off-screen is allowed) so hover
    /// expand/collapse cannot break BR pairing and walk the pinned rest.
    /// </summary>
    public static (int X, int Y) TransitionEnd(
        CompactRestOrigin rest,
        bool goingFull,
        double fullWidth,
        double fullHeight,
        IReadOnlyList<(int X, int Y, int Width, int Height)> workingAreas,
        double? settingsAnchorBottom)
    {
        _ = workingAreas;
        return TransitionAnimEnd(rest, goingFull, fullWidth, fullHeight, settingsAnchorBottom);
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

    /// <summary>
    /// Resolves the compact-rest origin for an expand/collapse transition.
    /// Collapse always uses the live frame's bottom-right so a stale rest cannot
    /// teleport the pill to a previous (e.g. screen-corner) origin.
    /// Expand uses the live pill when progress says rest OR Bounds already match compact size.
    /// </summary>
    public static CompactRestOrigin ResolveRestForTransition(
        bool goingFull,
        bool atCompactRest,
        int? currentRestX,
        int? currentRestY,
        int positionX,
        int positionY,
        double boundsWidth,
        double boundsHeight,
        double compactWidth,
        double compactHeight)
    {
        if (!goingFull)
        {
            var (x, y) = CollapseEnd(
                positionX,
                positionY,
                boundsWidth,
                boundsHeight,
                compactWidth,
                compactHeight);
            return new CompactRestOrigin(x, y, compactWidth, compactHeight);
        }

        // Prefer the live pill whenever the window is already compact-sized, even if
        // _compactProgress is stale (e.g. after a layout-delayed size apply).
        if (atCompactRest || IsVisuallyCompact(boundsWidth, boundsHeight, compactWidth, compactHeight))
            return new CompactRestOrigin(positionX, positionY, compactWidth, compactHeight);

        if (currentRestX is int restX && currentRestY is int restY)
            return new CompactRestOrigin(restX, restY, compactWidth, compactHeight);

        var (seedX, seedY) = CollapseEnd(
            positionX,
            positionY,
            boundsWidth,
            boundsHeight,
            compactWidth,
            compactHeight);
        return new CompactRestOrigin(seedX, seedY, compactWidth, compactHeight);
    }

    public static bool IsVisuallyCompact(
        double boundsWidth,
        double boundsHeight,
        double compactWidth,
        double compactHeight) =>
        Math.Abs(boundsWidth - compactWidth) < 8
        && Math.Abs(boundsHeight - compactHeight) < 8;

    /// <summary>
    /// Repositions a compact-rest origin so a size change keeps the bottom-right corner fixed.
    /// </summary>
    public static CompactRestOrigin ResizeKeepingBottomRight(
        CompactRestOrigin rest,
        double newWidth,
        double newHeight)
    {
        var (x, y) = WindowAnchorHelper.PlaceKeepingBottomRight(
            rest.X + rest.Width,
            rest.Y + rest.Height,
            newWidth,
            newHeight);
        return new CompactRestOrigin(x, y, newWidth, newHeight);
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

namespace DeezFuelGauge.Services;

public readonly record struct CompactAnimSample(
    double Width,
    double Height,
    double X,
    double Y);

/// <summary>
/// One sample of a bottom-right-pinned scale transition. Window geometry is fixed to the host
/// rect for mid frames; only scale/progress animate.
/// </summary>
public readonly record struct CompactScaleFrame(
    double WindowWidth,
    double WindowHeight,
    int WindowX,
    int WindowY,
    double ScaleX,
    double ScaleY,
    double Progress,
    bool MutatesWindowGeometry);

public static class CompactLayoutAnimator
{
    public static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(220);
    public static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(170);
    public const double FullFadeStart = 0.20;
    public const double CompactCullThreshold = 0.95;
    public const double FullCullThreshold = 0.05;

    public static double EaseOutCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - Math.Pow(1 - t, 3);
    }

    public static double EaseInCubic(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * t;
    }

    public static double EaseOutQuad(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return 1 - (1 - t) * (1 - t);
    }

    public static double EaseInQuad(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t;
    }

    public static double ApplyEase(double linearT, bool expanding) =>
        expanding ? EaseOutQuad(linearT) : EaseInQuad(linearT);

    public static double Lerp(double a, double b, double t) => a + (b - a) * t;

    public static double CompactOpacity(double progress) =>
        Math.Clamp(1 - progress, 0, 1);

    public static double FullOpacity(double progress)
    {
        if (progress <= FullFadeStart)
            return 0;

        return Math.Clamp((progress - FullFadeStart) / (1 - FullFadeStart), 0, 1);
    }

    public static bool ShouldRenderCompact(double progress) =>
        progress < CompactCullThreshold;

    public static bool ShouldRenderFull(double progress) =>
        progress > FullCullThreshold;

    public static TimeSpan DurationFor(double fromProgress, double toProgress)
    {
        var remaining = Math.Clamp(Math.Abs(toProgress - fromProgress), 0, 1);
        var full = toProgress > fromProgress ? ExpandDuration : CollapseDuration;
        return TimeSpan.FromMilliseconds(Math.Max(1, full.TotalMilliseconds * remaining));
    }

    public static CompactAnimSample Interpolate(
        CompactAnimSample start,
        CompactAnimSample end,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return end;

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return new CompactAnimSample(
            Lerp(start.Width, end.Width, t),
            Lerp(start.Height, end.Height, t),
            Lerp(start.X, end.X, t),
            Lerp(start.Y, end.Y, t));
    }

    /// <summary>
    /// Interpolates width/height only. Callers that pin bottom-right should derive X/Y from the
    /// fixed corner after this returns.
    /// </summary>
    public static CompactAnimSample InterpolateSize(
        CompactAnimSample start,
        CompactAnimSample end,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return new CompactAnimSample(end.Width, end.Height, 0, 0);

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return new CompactAnimSample(
            Lerp(start.Width, end.Width, t),
            Lerp(start.Height, end.Height, t),
            0,
            0);
    }

    /// <summary>
    /// Window host size for a scale transition. Uses the larger of the two endpoints so a
    /// bottom-right scale never clips.
    /// </summary>
    public static (double Width, double Height) HostSizeForTransition(
        CompactAnimSample a,
        CompactAnimSample b) =>
        (Math.Max(a.Width, b.Width), Math.Max(a.Height, b.Height));

    public static (double ScaleX, double ScaleY) ScaleFactorsForSize(
        double contentWidth,
        double contentHeight,
        double hostWidth,
        double hostHeight)
    {
        var sx = hostWidth <= 0 ? 1 : contentWidth / hostWidth;
        var sy = hostHeight <= 0 ? 1 : contentHeight / hostHeight;
        return (sx, sy);
    }

    public static (double ScaleX, double ScaleY) InterpolateScale(
        double fromScaleX,
        double fromScaleY,
        double toScaleX,
        double toScaleY,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return (toScaleX, toScaleY);

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return (Lerp(fromScaleX, toScaleX, t), Lerp(fromScaleY, toScaleY, t));
    }

    /// <summary>
    /// Census helper. Builds scale frames with window geometry fixed after the opening host set.
    /// Collapse also marks the final snap to the compact rect as a geometry mutation.
    /// </summary>
    public static IReadOnlyList<CompactScaleFrame> SampleScaleFrames(
        CompactAnimSample from,
        CompactAnimSample to,
        (double Width, double Height) host,
        int frameCount,
        bool expanding)
    {
        if (frameCount < 1)
            frameCount = 1;

        var (anchorRight, anchorBottom) = WindowAnchorHelper.GetBottomRight(
            from.X, from.Y, from.Width, from.Height);
        var (hostX, hostY) = WindowAnchorHelper.ComputeBottomRightAnchoredPosition(
            anchorRight, anchorBottom, host.Width, host.Height);
        var (fromScaleX, fromScaleY) = ScaleFactorsForSize(from.Width, from.Height, host.Width, host.Height);
        var (toScaleX, toScaleY) = ScaleFactorsForSize(to.Width, to.Height, host.Width, host.Height);

        var frames = new List<CompactScaleFrame>(frameCount + 1);
        for (var i = 0; i <= frameCount; i++)
        {
            var linearT = i / (double)frameCount;
            var (scaleX, scaleY) = InterpolateScale(
                fromScaleX, fromScaleY, toScaleX, toScaleY, linearT, expanding, reduceMotion: false);
            var progress = InterpolateProgress(
                expanding ? 0 : 1,
                expanding ? 1 : 0,
                linearT,
                expanding,
                reduceMotion: false);

            var isFirst = i == 0;
            var isLast = i == frameCount;
            var mutates = isFirst || (isLast && !expanding);
            var width = host.Width;
            var height = host.Height;
            var x = hostX;
            var y = hostY;
            if (isLast && !expanding)
            {
                width = to.Width;
                height = to.Height;
                (x, y) = WindowAnchorHelper.ComputeBottomRightAnchoredPosition(
                    anchorRight, anchorBottom, width, height);
            }

            frames.Add(new CompactScaleFrame(
                width, height, x, y, scaleX, scaleY, progress, mutates));
        }

        return frames;
    }

    public static double InterpolateProgress(
        double startProgress,
        double endProgress,
        double linearT,
        bool expanding,
        bool reduceMotion)
    {
        if (reduceMotion || linearT >= 1)
            return endProgress;

        var t = ApplyEase(Math.Clamp(linearT, 0, 1), expanding);
        return Lerp(startProgress, endProgress, t);
    }
}

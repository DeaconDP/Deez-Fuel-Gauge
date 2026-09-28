namespace DeezFuelGauge.Services;

public readonly record struct CompactAnimSample(
    double Width,
    double Height,
    double X,
    double Y);

public static class CompactLayoutAnimator
{
    public static readonly TimeSpan ExpandDuration = TimeSpan.FromMilliseconds(240);
    public static readonly TimeSpan CollapseDuration = TimeSpan.FromMilliseconds(200);
    public const double FullFadeStart = 0;
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

    public static double ApplyEase(double linearT, bool expanding)
    {
        // Ease-out both ways. Ease-in collapse back-loaded motion into a short tail that
        // read as staccato when frames were sparse.
        _ = expanding;
        return EaseOutQuad(linearT);
    }

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

    /// <summary>
    /// Advances animation elapsed time by the real frame gap so sparse frames under
    /// load catch up instead of slow-motioning (do not clamp to ~16–50ms).
    /// </summary>
    public static double AdvanceElapsedMs(double elapsedMs, double rawDeltaMs) =>
        elapsedMs + Math.Max(0, rawDeltaMs);

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

    public static bool TryCreateScaleHost(
        CompactAnimSample start,
        CompactAnimSample end,
        out CompactScaleHost host)
    {
        var startRight = start.X + start.Width;
        var endRight = end.X + end.Width;
        var startBottom = start.Y + start.Height;
        var endBottom = end.Y + end.Height;
        if (Math.Abs(startRight - endRight) > 1.5
            || Math.Abs(startBottom - endBottom) > 1.5
            || start.Width < 1
            || start.Height < 1
            || end.Width < 1
            || end.Height < 1)
        {
            host = default;
            return false;
        }

        var right = (startRight + endRight) / 2;
        var bottom = (startBottom + endBottom) / 2;
        var width = Math.Max(start.Width, end.Width);
        var height = Math.Max(start.Height, end.Height);
        host = new CompactScaleHost(
            (int)Math.Round(right - width),
            (int)Math.Round(bottom - height),
            width,
            height,
            start.Width / width,
            start.Height / height,
            end.Width / width,
            end.Height / height);
        return true;
    }

    /// <summary>
    /// Compact transitions must not rewrite Position/Width/Height every frame.
    /// Prefer a scale host; otherwise opacity-only until a single snap at the end.
    /// </summary>
    public static bool ShouldRewriteWindowGeometryEachFrame() => false;

    /// <summary>
    /// Windows DWM hitchs on transparent HWND resize. Keep one full host for the
    /// whole compact session and animate scale only. Rest uses a BR-aligned compact
    /// pill chrome (plus Win32 region clip) so minimise is not a scaled-down full widget.
    /// </summary>
    public static bool PreferStableCompactHostGeometry { get; } = OperatingSystem.IsWindows();

    public static bool ShouldShrinkWindowToCompactRest() => !PreferStableCompactHostGeometry;

    public static bool ShouldResizeWindowForCompactTransition() => !PreferStableCompactHostGeometry;

    public const double ScaleSettleEpsilon = 0.02;
    public const int MaxFinishDefers = 30;

    /// <summary>
    /// True when the live scale is close enough to the host target that snapping
    /// window geometry will not hitch. Collapse uses an ease-in, so finishing early
    /// cuts the motion-heavy tail and looks like a stutter.
    /// </summary>
    public static bool IsScaleSettled(
        double scaleX,
        double scaleY,
        double toScaleX,
        double toScaleY,
        double epsilon = ScaleSettleEpsilon) =>
        Math.Abs(scaleX - toScaleX) <= epsilon
        && Math.Abs(scaleY - toScaleY) <= epsilon;

    /// <summary>
    /// Finish timer must wait for the scale transition when a host is active.
    /// Caps deferrals so a stuck transition cannot loop forever.
    /// </summary>
    public static bool ShouldDeferCompactFinish(
        bool hasScaleHost,
        double scaleX,
        double scaleY,
        double toScaleX,
        double toScaleY,
        int deferCount,
        int maxDefers = MaxFinishDefers)
    {
        if (!hasScaleHost || deferCount >= maxDefers)
            return false;

        return !IsScaleSettled(scaleX, scaleY, toScaleX, toScaleY);
    }
}

public readonly record struct CompactScaleHost(
    int X,
    int Y,
    double Width,
    double Height,
    double FromScaleX,
    double FromScaleY,
    double ToScaleX,
    double ToScaleY)
{
    public (double ScaleX, double ScaleY) ScaleAt(double easedT) =>
        (CompactLayoutAnimator.Lerp(FromScaleX, ToScaleX, easedT),
         CompactLayoutAnimator.Lerp(FromScaleY, ToScaleY, easedT));
}

using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

/// <summary>
/// Rerunnable census of compact expand/collapse geometry.
/// Locks two failure modes: wandering bottom-right, and per-frame OS window resize.
/// </summary>
public sealed class CompactScaleCensusTests
{
    private static readonly CompactAnimSample Compact = new(140, 36, 1600, 900);
    private static readonly CompactAnimSample Full = new(300, 260, 1440, 676);

    [Fact]
    public void Independent_xy_lerp_lets_bottom_right_wander()
    {
        // Old edge-aware path sometimes kept top-left fixed. That end sample makes BR wander.
        var tlPinnedEnd = new CompactAnimSample(Full.Width, Full.Height, Compact.X, Compact.Y);
        var (anchorRight, anchorBottom) = WindowAnchorHelper.GetBottomRight(
            Compact.X, Compact.Y, Compact.Width, Compact.Height);

        var maxDrift = 0.0;
        for (var i = 0; i <= 20; i++)
        {
            var linearT = i / 20.0;
            var sample = CompactLayoutAnimator.Interpolate(
                Compact, tlPinnedEnd, linearT, expanding: true, reduceMotion: false);
            var right = sample.X + sample.Width;
            var bottom = sample.Y + sample.Height;
            maxDrift = Math.Max(maxDrift, Math.Abs(right - anchorRight));
            maxDrift = Math.Max(maxDrift, Math.Abs(bottom - anchorBottom));
        }

        Assert.True(maxDrift > 1.0, $"expected wander, saw maxDrift={maxDrift}");
    }

    [Fact]
    public void Bottom_right_derived_position_keeps_corner_stable_across_frames()
    {
        var (anchorRight, anchorBottom) = WindowAnchorHelper.GetBottomRight(
            Compact.X, Compact.Y, Compact.Width, Compact.Height);
        var end = new CompactAnimSample(Full.Width, Full.Height, 0, 0);

        var maxDrift = 0.0;
        for (var i = 0; i <= 20; i++)
        {
            var linearT = i / 20.0;
            var sized = CompactLayoutAnimator.InterpolateSize(
                Compact, end, linearT, expanding: true, reduceMotion: false);
            var (x, y) = WindowAnchorHelper.ComputeBottomRightAnchoredPosition(
                anchorRight, anchorBottom, sized.Width, sized.Height);
            maxDrift = Math.Max(maxDrift, Math.Abs(x + sized.Width - anchorRight));
            maxDrift = Math.Max(maxDrift, Math.Abs(y + sized.Height - anchorBottom));
        }

        Assert.True(maxDrift <= 0.5, $"BR drifted {maxDrift}px");
    }

    [Fact]
    public void Per_frame_window_resize_path_mutates_geometry_every_sample()
    {
        // Documents the lag actor: ApplyCompactFrame writes Width/Height/Position each RAF.
        var (anchorRight, anchorBottom) = WindowAnchorHelper.GetBottomRight(
            Compact.X, Compact.Y, Compact.Width, Compact.Height);
        var end = new CompactAnimSample(Full.Width, Full.Height, 0, 0);

        var geometryMutations = 0;
        double? prevW = null, prevH = null;
        int? prevX = null, prevY = null;
        for (var i = 0; i <= 20; i++)
        {
            var linearT = i / 20.0;
            var sized = CompactLayoutAnimator.InterpolateSize(
                Compact, end, linearT, expanding: true, reduceMotion: false);
            var (x, y) = WindowAnchorHelper.ComputeBottomRightAnchoredPosition(
                anchorRight, anchorBottom, sized.Width, sized.Height);

            if (prevW is null
                || Math.Abs(prevW.Value - sized.Width) > 0.01
                || Math.Abs(prevH!.Value - sized.Height) > 0.01
                || prevX != x
                || prevY != y)
            {
                geometryMutations++;
            }

            prevW = sized.Width;
            prevH = sized.Height;
            prevX = x;
            prevY = y;
        }

        Assert.True(
            geometryMutations > 10,
            $"census: resize-every-frame path mutated geometry {geometryMutations} times");
    }

    [Fact]
    public void Scale_host_path_mutates_window_geometry_at_most_twice()
    {
        // Desired path: size window once to the host (larger) rect, animate scale, snap once at end.
        var host = CompactLayoutAnimator.HostSizeForTransition(Compact, Full);
        var frames = CompactLayoutAnimator.SampleScaleFrames(
            Compact, Full, host, frameCount: 20, expanding: true);

        var geometryMutations = frames.Count(f => f.MutatesWindowGeometry);
        Assert.True(
            geometryMutations <= 2,
            $"expected <=2 window geometry mutations, saw {geometryMutations}");

        foreach (var frame in frames.Where(f => !f.MutatesWindowGeometry))
        {
            Assert.Equal(host.Width, frame.WindowWidth);
            Assert.Equal(host.Height, frame.WindowHeight);
            Assert.Equal(frames[0].WindowX, frame.WindowX);
            Assert.Equal(frames[0].WindowY, frame.WindowY);
        }

        var (anchorRight, anchorBottom) = WindowAnchorHelper.GetBottomRight(
            Compact.X, Compact.Y, Compact.Width, Compact.Height);
        Assert.Equal(anchorRight, frames[0].WindowX + frames[0].WindowWidth);
        Assert.Equal(anchorBottom, frames[0].WindowY + frames[0].WindowHeight);
    }
}

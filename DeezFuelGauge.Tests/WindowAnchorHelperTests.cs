using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class WindowAnchorHelperTests
{
    [Theory]
    [InlineData(100, 150, 200, 150)]
    [InlineData(200, 150, 100, 150)]
    [InlineData(100, 100, 50, 50)]
    public void CompensateVerticalGrowth_moves_top_up_when_height_increases(
        double oldHeight,
        double newHeight,
        int currentY,
        int expectedY)
    {
        var result = WindowAnchorHelper.CompensateVerticalGrowth(oldHeight, newHeight, currentY);
        Assert.Equal(expectedY, result);
    }

    [Theory]
    [InlineData(0, 0, 1920, 1080, 300, 400, 810, 340)]
    [InlineData(100, 50, 800, 600, 300, 400, 350, 150)]
    public void ComputeCenteredPosition_centers_window_in_working_area(
        int workAreaX,
        int workAreaY,
        int workAreaWidth,
        int workAreaHeight,
        int windowWidth,
        int windowHeight,
        int expectedX,
        int expectedY)
    {
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            workAreaX, workAreaY, workAreaWidth, workAreaHeight, windowWidth, windowHeight);

        Assert.Equal(expectedX, x);
        Assert.Equal(expectedY, y);
    }

    [Theory]
    [InlineData(300, 200, 100)]
    [InlineData(450.5, 280.5, 170)]
    public void ComputeBottomAnchoredY_keeps_bottom_edge_fixed(double anchorBottom, double height, int expectedY)
    {
        var result = WindowAnchorHelper.ComputeBottomAnchoredY(anchorBottom, height);
        Assert.Equal(expectedY, result);
        Assert.Equal(anchorBottom, result + height, precision: 5);
    }

    [Theory]
    [InlineData(200, 100, 150, 150)] // grow: Y moves up by 50
    [InlineData(200, 150, 100, 250)] // shrink: Y moves down by 50
    [InlineData(40, 36, 260, -184)] // near top of screen still grows upward
    public void ResolveSettingsExpandEndY_keeps_bottom_edge_fixed(
        int currentY,
        double currentHeight,
        double newHeight,
        int expectedY)
    {
        var result = WindowAnchorHelper.ResolveSettingsExpandEndY(currentY, currentHeight, newHeight);
        Assert.Equal(expectedY, result);
        Assert.Equal(currentY + currentHeight, result + newHeight, precision: 5);
    }

    [Fact]
    public void ResolveSettingsExpandEndY_prefers_bottom_lock_even_when_nearer_top_than_CompensateSizeChange()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        const int currentX = 40;
        const int currentY = 40;
        const double currentHeight = 36;
        const double newHeight = 260;

        var (_, edgeAwareY) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: currentHeight,
            newWidth: 300,
            newHeight: newHeight,
            currentX: currentX,
            currentY: currentY,
            areas);

        var settingsY = WindowAnchorHelper.ResolveSettingsExpandEndY(currentY, currentHeight, newHeight);

        Assert.Equal(40, edgeAwareY); // edge-aware keeps top when near top
        Assert.Equal(40 + 36 - 260, settingsY); // settings always bottom-locks
        Assert.True(settingsY < edgeAwareY);
    }

    [Fact]
    public void ComputeCenteredPosition_clamps_to_work_area_origin_when_window_is_larger()
    {
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            100, 50, 800, 600, 900, 700);

        Assert.Equal(100, x);
        Assert.Equal(50, y);
    }

    [Fact]
    public void ClampToWorkingAreas_keeps_position_when_already_visible()
    {
        var areas = new[] { (0, 0, 1728, 1117) };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(1200, 600, 300, 254, areas);

        Assert.Equal(1200, x);
        Assert.Equal(600, y);
    }

    [Fact]
    public void ClampToWorkingAreas_pulls_offscreen_pinned_window_onto_display()
    {
        // Saved coords from a disconnected secondary monitor (below the laptop panel).
        var areas = new[] { (0, 0, 1728, 1117) };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(1428, 1353, 300, 254, areas);

        Assert.Equal(1428, x);
        Assert.Equal(1117 - 254, y);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(x, y, 300, 254, areas[0]));
    }

    [Fact]
    public void ClampToWorkingAreas_chooses_nearest_of_multiple_displays()
    {
        var areas = new[]
        {
            (0, 0, 1728, 1117),
            (1728, 0, 1920, 1080)
        };

        var (x, y) = WindowAnchorHelper.ClampToWorkingAreas(4000, 40, 300, 254, areas);

        Assert.Equal(1728 + 1920 - 300, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void CompensateSizeChange_grows_down_and_right_when_away_from_far_edges()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 260,
            currentX: 40,
            currentY: 40,
            areas);

        Assert.Equal(40, x);
        Assert.Equal(40, y);
    }

    [Fact]
    public void CompensateSizeChange_keeps_right_and_bottom_when_near_those_edges()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 260,
            currentX: 1920 - 140 - 10,
            currentY: 1080 - 36 - 10,
            areas);

        Assert.Equal(1920 - 300 - 10, x);
        Assert.Equal(1080 - 260 - 10, y);
    }

    [Fact]
    public void CompensateSizeChange_clamps_into_working_area_when_expanding_off_top()
    {
        var areas = new[] { (0, 0, 1920, 1080) };

        var (x, y) = WindowAnchorHelper.CompensateSizeChange(
            oldWidth: 140,
            oldHeight: 36,
            newWidth: 300,
            newHeight: 400,
            currentX: 10,
            currentY: 10,
            areas);

        Assert.Equal(10, x);
        Assert.Equal(10, y);
        Assert.True(WindowAnchorHelper.HasVisibleOverlap(x, y, 300, 400, areas[0]));
    }

    [Fact]
    public void TransitionEnd_minify_keeps_the_bottom_right_corner_near_the_top_left()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        var rest = new CompactRestOrigin(40, 40, 140, 36);

        var expanded = CompactPlacement.TransitionEnd(rest, true, 300, 260, areas, null);
        var collapsed = CompactPlacement.TransitionEnd(rest, false, 300, 260, areas, null);

        Assert.Equal(-120, expanded.X);
        Assert.Equal(-184, expanded.Y);
        Assert.Equal(40, collapsed.X);
        Assert.Equal(40, collapsed.Y);
        Assert.Equal(expanded.X + 300, collapsed.X + 140);
        Assert.Equal(expanded.Y + 260, collapsed.Y + 36);
    }

    [Fact]
    public void CollapseEnd_moves_the_top_left_down_to_the_live_bottom_right()
    {
        var (x, y) = CompactPlacement.CollapseEnd(40, 40, 300, 260, 140, 36);

        Assert.Equal(200, x);
        Assert.Equal(264, y);
        Assert.Equal(40 + 300, x + 140);
        Assert.Equal(40 + 260, y + 36);
    }

    [Fact]
    public void TransitionEnd_returns_to_the_compact_origin_after_expand()
    {
        var areas = new[] { (0, 0, 2048, 1104) };
        const double compactW = 140;
        const double compactH = 48;
        const double fullW = 300;
        const double fullH = 640;

        var walked = new List<string>();
        for (var y = 0; y <= 1000; y += 40)
        {
            for (var x = 0; x <= 1800; x += 40)
            {
                var rest = new CompactRestOrigin(x, y, compactW, compactH);
                var expanded = CompactPlacement.TransitionEnd(rest, true, fullW, fullH, areas, null);
                var collapsed = CompactPlacement.TransitionEnd(rest, false, fullW, fullH, areas, null);
                var expectedX = (int)Math.Round(x + compactW - fullW);
                var expectedY = (int)Math.Round(y + compactH - fullH);
                if (collapsed.X != x || collapsed.Y != y || expanded.X != expectedX || expanded.Y != expectedY)
                    walked.Add($"({x},{y}) -> ({expanded.X},{expanded.Y}) -> ({collapsed.X},{collapsed.Y})");
                else if (expanded.X + fullW != collapsed.X + compactW || expanded.Y + fullH != collapsed.Y + compactH)
                    walked.Add($"BR break at ({x},{y})");
            }
        }

        Assert.True(walked.Count == 0, string.Join("\n", walked.Take(8)));
    }

    [Fact]
    public void TransitionEnd_pins_bottom_right_when_the_compact_pill_is_near_the_top()
    {
        var areas = new[] { (0, 0, 2048, 1104) };
        var rest = new CompactRestOrigin(880, 0, 140, 48);

        var expanded = CompactPlacement.TransitionEnd(rest, true, 300, 640, areas, null);
        var collapsed = CompactPlacement.TransitionEnd(rest, false, 300, 640, areas, null);

        Assert.Equal(720, expanded.X);
        Assert.Equal(-592, expanded.Y);
        Assert.Equal(880, collapsed.X);
        Assert.Equal(0, collapsed.Y);
        Assert.Equal(expanded.X + 300, collapsed.X + 140);
        Assert.Equal(expanded.Y + 640, collapsed.Y + 48);
    }

    [Fact]
    public void TransitionEnd_bottom_locks_settings_and_still_collapses_home()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        var rest = new CompactRestOrigin(40, 40, 140, 36);

        var expanded = CompactPlacement.TransitionEnd(rest, true, 300, 260, areas, settingsAnchorBottom: 76);
        var collapsed = CompactPlacement.TransitionEnd(rest, false, 300, 260, areas, settingsAnchorBottom: 76);

        Assert.Equal(-120, expanded.X);
        Assert.Equal(76 - 260, expanded.Y);
        Assert.Equal(40, collapsed.X);
        Assert.Equal(40, collapsed.Y);
    }

    [Fact]
    public void TransitionEnd_near_origin_keeps_bottom_right_and_collapses_home()
    {
        var areas = new[] { (0, 0, 1920, 1080) };
        var rest = new CompactRestOrigin(0, 0, 140, 36);

        var expanded = CompactPlacement.TransitionEnd(rest, true, 300, 400, areas, null);
        var collapsed = CompactPlacement.TransitionEnd(rest, false, 300, 400, areas, null);

        Assert.Equal(-160, expanded.X);
        Assert.Equal(-364, expanded.Y);
        Assert.Equal(0, collapsed.X);
        Assert.Equal(0, collapsed.Y);
        Assert.Equal(expanded.X + 300, collapsed.X + 140);
        Assert.Equal(expanded.Y + 400, collapsed.Y + 36);
    }

    [Fact]
    public void AfterExpandedDrag_moves_the_compact_origin_by_the_same_delta()
    {
        var rest = new CompactRestOrigin(100, 200, 140, 48);

        var moved = CompactPlacement.AfterExpandedDrag(rest, placedX: 100, placedY: 80, draggedX: 130, draggedY: 50);

        Assert.Equal(130, moved.X);
        Assert.Equal(170, moved.Y);
        Assert.Equal(140, moved.Width);
        Assert.Equal(48, moved.Height);
    }

    [Fact]
    public void Layout_shift_skips_a_frame_the_compact_transition_owns()
    {
        Assert.False(CompactPlacement.ShouldApplyLayoutShift(
            pending: true,
            transitionOwnsFrame: true,
            anchorFromHeight: 640,
            newHeight: 48));
        Assert.True(CompactPlacement.ShouldApplyLayoutShift(
            pending: true,
            transitionOwnsFrame: false,
            anchorFromHeight: 400,
            newHeight: 460));
    }

    [Fact]
    public void TransitionAnimEnd_matches_TransitionEnd_without_clamping()
    {
        var rest = new CompactRestOrigin(0, 0, 140, 36);
        var areas = new[] { (0, 0, 1920, 1080) };

        var anim = CompactPlacement.TransitionAnimEnd(rest, true, 300, 400, null);
        var final = CompactPlacement.TransitionEnd(rest, true, 300, 400, areas, null);

        Assert.Equal(-160, anim.X);
        Assert.Equal(-364, anim.Y);
        Assert.Equal(140, anim.X + 300);
        Assert.Equal(36, anim.Y + 400);
        Assert.Equal(anim, final);
    }

    [Fact]
    public void TransitionAnimEnd_allows_scale_host_from_compact_rest()
    {
        var rest = new CompactRestOrigin(0, 0, 140, 36);
        var areas = new[] { (0, 0, 1920, 1080) };
        var start = new CompactAnimSample(140, 36, rest.X, rest.Y);

        var anim = CompactPlacement.TransitionAnimEnd(rest, true, 300, 400, null);
        var final = CompactPlacement.TransitionEnd(rest, true, 300, 400, areas, null);
        var animEnd = new CompactAnimSample(300, 400, anim.X, anim.Y);
        var finalEnd = new CompactAnimSample(300, 400, final.X, final.Y);

        Assert.True(CompactLayoutAnimator.TryCreateScaleHost(start, animEnd, out _));
        Assert.True(CompactLayoutAnimator.TryCreateScaleHost(start, finalEnd, out _));
    }

    [Fact]
    public void ResizeKeepingBottomRight_grows_toward_the_top_left()
    {
        var rest = new CompactRestOrigin(200, 300, 100, 40);

        var resized = CompactPlacement.ResizeKeepingBottomRight(rest, 160, 56);

        Assert.Equal(140, resized.X);
        Assert.Equal(284, resized.Y);
        Assert.Equal(160, resized.Width);
        Assert.Equal(56, resized.Height);
        Assert.Equal(rest.X + rest.Width, resized.X + resized.Width, 5);
        Assert.Equal(rest.Y + rest.Height, resized.Y + resized.Height, 5);
    }

    [Fact]
    public void ResolveRestForTransition_collapse_ignores_stale_screen_corner_rest()
    {
        const int expandedX = 400;
        const int expandedY = 300;
        const double expandedW = 300;
        const double expandedH = 260;
        const double compactW = 140;
        const double compactH = 36;
        // Stale rest parked at the working-area bottom-right.
        const int staleRestX = 1780;
        const int staleRestY = 1044;

        var resolved = CompactPlacement.ResolveRestForTransition(
            goingFull: false,
            atCompactRest: false,
            currentRestX: staleRestX,
            currentRestY: staleRestY,
            positionX: expandedX,
            positionY: expandedY,
            boundsWidth: expandedW,
            boundsHeight: expandedH,
            compactWidth: compactW,
            compactHeight: compactH);

        var expected = CompactPlacement.CollapseEnd(
            expandedX, expandedY, expandedW, expandedH, compactW, compactH);

        Assert.Equal(expected.X, resolved.X);
        Assert.Equal(expected.Y, resolved.Y);
        Assert.NotEqual(staleRestX, resolved.X);
        Assert.NotEqual(staleRestY, resolved.Y);
        Assert.Equal(expandedX + expandedW, resolved.X + compactW, 5);
        Assert.Equal(expandedY + expandedH, resolved.Y + compactH, 5);
    }

    [Fact]
    public void ResolveRestForTransition_expand_keeps_existing_rest()
    {
        var resolved = CompactPlacement.ResolveRestForTransition(
            goingFull: true,
            atCompactRest: false,
            currentRestX: 80,
            currentRestY: 90,
            positionX: 10,
            positionY: 20,
            boundsWidth: 300,
            boundsHeight: 260,
            compactWidth: 140,
            compactHeight: 36);

        Assert.Equal(80, resolved.X);
        Assert.Equal(90, resolved.Y);
    }

    [Fact]
    public void ResolveRestForTransition_expand_at_compact_rest_uses_live_pill()
    {
        var resolved = CompactPlacement.ResolveRestForTransition(
            goingFull: true,
            atCompactRest: true,
            currentRestX: 80,
            currentRestY: 90,
            positionX: 120,
            positionY: 140,
            boundsWidth: 140,
            boundsHeight: 36,
            compactWidth: 140,
            compactHeight: 36);

        Assert.Equal(120, resolved.X);
        Assert.Equal(140, resolved.Y);
    }

    [Fact]
    public void ResolveRestForTransition_expand_with_missing_rest_seeds_from_live_br()
    {
        var resolved = CompactPlacement.ResolveRestForTransition(
            goingFull: true,
            atCompactRest: false,
            currentRestX: null,
            currentRestY: null,
            positionX: 40,
            positionY: 40,
            boundsWidth: 300,
            boundsHeight: 260,
            compactWidth: 140,
            compactHeight: 36);

        Assert.Equal(200, resolved.X);
        Assert.Equal(264, resolved.Y);
    }

    [Fact]
    public void ResolveRestForTransition_expand_uses_live_pill_when_bounds_match_compact()
    {
        // Stale rest Y (like progress=1 after a layout-delayed collapse) must not win
        // when the window is already compact-sized at a different origin.
        var resolved = CompactPlacement.ResolveRestForTransition(
            goingFull: true,
            atCompactRest: false,
            currentRestX: 2449,
            currentRestY: 1253,
            positionX: 2449,
            positionY: 1166,
            boundsWidth: 92.8,
            boundsHeight: 124.8,
            compactWidth: 93,
            compactHeight: 125);

        Assert.Equal(2449, resolved.X);
        Assert.Equal(1166, resolved.Y);
    }
}

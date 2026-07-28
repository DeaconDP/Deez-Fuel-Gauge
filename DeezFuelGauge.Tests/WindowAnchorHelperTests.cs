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
}

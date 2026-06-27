using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

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

    [Fact]
    public void ComputeCenteredPosition_clamps_to_work_area_origin_when_window_is_larger()
    {
        var (x, y) = WindowAnchorHelper.ComputeCenteredPosition(
            100, 50, 800, 600, 900, 700);

        Assert.Equal(100, x);
        Assert.Equal(50, y);
    }
}

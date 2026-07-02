using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class ProviderBarPresenterTests
{
    [Theory]
    [InlineData(100, 0, false, 0)]
    [InlineData(100, 10, false, 10)]
    public void ComputeFillWidth_returns_zero_when_not_ready_at_zero_percent(
        double trackWidth,
        double percentUsed,
        bool showReadySliver,
        double expected)
    {
        Assert.Equal(expected, ProviderBarPresenter.ComputeFillWidth(trackWidth, percentUsed, showReadySliver));
    }

    [Fact]
    public void ComputeFillWidth_returns_min_sliver_when_ready_at_zero_percent()
    {
        Assert.Equal(
            ProviderBarPresenter.ReadySliverMinWidth,
            ProviderBarPresenter.ComputeFillWidth(100, 0, showReadySliver: true));
    }

    [Theory]
    [InlineData(100, 50, 50)]
    [InlineData(200, 25, 50)]
    public void ComputeFillWidth_uses_actual_width_when_percent_above_zero(
        double trackWidth,
        double percentUsed,
        double expected)
    {
        Assert.Equal(expected, ProviderBarPresenter.ComputeFillWidth(trackWidth, percentUsed, showReadySliver: true));
    }

    [Fact]
    public void ComputeFillWidth_returns_zero_when_track_width_is_zero()
    {
        Assert.Equal(0, ProviderBarPresenter.ComputeFillWidth(0, 0, showReadySliver: true));
    }
}

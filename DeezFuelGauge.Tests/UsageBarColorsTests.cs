using Avalonia.Media;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class UsageBarColorsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(24.9)]
    public void GetColorForPercent_returns_blue_below_25(double percent)
    {
        var color = UsageBarColors.GetColorForPercent(percent);

        Assert.Equal(Color.FromRgb(0x4D, 0x9F, 0xFF), color);
    }

    [Theory]
    [InlineData(25)]
    [InlineData(74.9)]
    public void GetColorForPercent_returns_green_from_25_to_below_75(double percent)
    {
        var color = UsageBarColors.GetColorForPercent(percent);

        Assert.Equal(Color.FromRgb(0x4C, 0xAF, 0x50), color);
    }

    [Theory]
    [InlineData(75)]
    [InlineData(89.9)]
    public void GetColorForPercent_returns_yellow_from_75_to_below_90(double percent)
    {
        var color = UsageBarColors.GetColorForPercent(percent);

        Assert.Equal(Color.FromRgb(0xFF, 0xEB, 0x3B), color);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(100)]
    public void GetColorForPercent_returns_orange_from_90_upward(double percent)
    {
        var color = UsageBarColors.GetColorForPercent(percent);

        Assert.Equal(Color.FromRgb(0xFF, 0x98, 0x00), color);
    }
}

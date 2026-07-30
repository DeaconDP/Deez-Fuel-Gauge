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

    [Theory]
    [InlineData(0)]
    [InlineData(24.9)]
    public void GetMutedColorForResetProgress_returns_blue_just_after_reset(double percent)
    {
        Assert.Equal(UsageBarColors.MutedResetBlue, UsageBarColors.GetMutedColorForResetProgress(percent));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(74.9)]
    public void GetMutedColorForResetProgress_returns_green_mid_window(double percent)
    {
        Assert.Equal(UsageBarColors.MutedResetGreen, UsageBarColors.GetMutedColorForResetProgress(percent));
    }

    [Theory]
    [InlineData(75)]
    [InlineData(100)]
    public void GetMutedColorForResetProgress_returns_yellow_when_reset_soon(double percent)
    {
        Assert.Equal(UsageBarColors.MutedResetYellow, UsageBarColors.GetMutedColorForResetProgress(percent));
    }

    [Fact]
    public void GetResetProgressPercent_is_zero_just_after_reset()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var resetsAt = now + UsageBarColors.FiveHourWindow;

        var progress = UsageBarColors.GetResetProgressPercent(resetsAt, UsageBarColors.FiveHourWindow, now);

        Assert.Equal(0, progress);
    }

    [Fact]
    public void GetResetProgressPercent_is_mid_halfway_through_window()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var resetsAt = now + TimeSpan.FromHours(2.5);

        var progress = UsageBarColors.GetResetProgressPercent(resetsAt, UsageBarColors.FiveHourWindow, now);

        Assert.Equal(50, progress);
    }

    [Fact]
    public void GetResetProgressPercent_is_100_when_reset_passed()
    {
        var now = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);
        var resetsAt = now.AddMinutes(-1);

        var progress = UsageBarColors.GetResetProgressPercent(resetsAt, UsageBarColors.FiveHourWindow, now);

        Assert.Equal(100, progress);
    }

    [Fact]
    public void GetResetProgressPercent_uses_explicit_window_start_and_end()
    {
        var start = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero);

        var progress = UsageBarColors.GetResetProgressPercent(start, end, now);

        Assert.Equal(50, progress);
    }
}

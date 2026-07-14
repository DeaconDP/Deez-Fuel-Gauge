using Avalonia;
using Avalonia.Media;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class DiskBarPresenterTests
{
    [Fact]
    public void CreateHatchBrush_uses_repeating_stripe_gradient()
    {
        var color = UsageBarColors.GetColorForPercent(50);
        var brush = Assert.IsType<LinearGradientBrush>(DiskBarPresenter.CreateHatchBrush(color));

        Assert.Equal(GradientSpreadMethod.Repeat, brush.SpreadMethod);
        Assert.Equal(RelativeUnit.Absolute, brush.StartPoint.Unit);
        Assert.Equal(RelativeUnit.Absolute, brush.EndPoint.Unit);
        Assert.Equal(DiskBarPresenter.HatchStripeWidth * 2, brush.EndPoint.Point.X);
        Assert.Equal(4, brush.GradientStops.Count);
        Assert.Equal(color, brush.GradientStops[0].Color);
    }

    [Fact]
    public void CreateTrack_uses_disk_chrome()
    {
        var (track, fill) = DiskBarPresenter.CreateTrack();

        Assert.Equal(DiskBarPresenter.TrackHeight, track.Height);
        Assert.Equal(new CornerRadius(1), fill.CornerRadius);
        Assert.True(fill.ClipToBounds);
        Assert.IsType<LinearGradientBrush>(fill.Background);
    }
}

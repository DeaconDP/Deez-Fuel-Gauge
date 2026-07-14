using Avalonia;
using Avalonia.Media;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class HardwareBarPresenterTests
{
    [Fact]
    public void CreateDiagonalHatchBrush_uses_repeating_diagonal_gradient()
    {
        var color = UsageBarColors.GetColorForPercent(50);
        var brush = Assert.IsType<LinearGradientBrush>(HardwareBarPresenter.CreateDiagonalHatchBrush(color));

        Assert.Equal(GradientSpreadMethod.Repeat, brush.SpreadMethod);
        Assert.Equal(RelativeUnit.Absolute, brush.StartPoint.Unit);
        Assert.Equal(RelativeUnit.Absolute, brush.EndPoint.Unit);
        Assert.Equal(HardwareBarPresenter.HatchStripeWidth, brush.EndPoint.Point.X);
        Assert.Equal(HardwareBarPresenter.HatchStripeWidth, brush.EndPoint.Point.Y);
        Assert.Equal(color, brush.GradientStops[0].Color);
    }

    [Fact]
    public void CreateTrack_uses_square_diagonal_fill()
    {
        var (track, fill) = HardwareBarPresenter.CreateTrack();

        Assert.Equal(HardwareBarPresenter.TrackHeight, track.Height);
        Assert.Equal(default(CornerRadius), fill.CornerRadius);
        Assert.True(fill.ClipToBounds);
        Assert.IsType<LinearGradientBrush>(fill.Background);
    }
}

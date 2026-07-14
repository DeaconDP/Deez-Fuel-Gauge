using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DeezFuelGauge.Services;

public static class DiskBarPresenter
{
    public const double TrackHeight = 6;
    public const double HatchStripeWidth = 3;

    private static readonly Color TrackBackdropColor = Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF);
    private static readonly Color TrackBorderColor = Color.FromArgb(0x55, 0xAA, 0xAA, 0xAA);

    public static IBrush CreateHatchBrush(Color baseColor)
    {
        var dark = Color.FromArgb(
            baseColor.A,
            (byte)(baseColor.R * 0.55),
            (byte)(baseColor.G * 0.55),
            (byte)(baseColor.B * 0.55));

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(HatchStripeWidth * 2, 0, RelativeUnit.Absolute),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            {
                new GradientStop(baseColor, 0),
                new GradientStop(baseColor, 0.48),
                new GradientStop(dark, 0.52),
                new GradientStop(dark, 1)
            }
        };
    }

    public static (Grid Track, Border Fill) CreateTrack()
    {
        var fill = new Border
        {
            CornerRadius = new CornerRadius(1),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
            ClipToBounds = true,
            Background = CreateHatchBrush(UsageBarColors.GetColorForPercent(0))
        };

        var track = new Grid
        {
            Height = TrackHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Border
                {
                    CornerRadius = new CornerRadius(1),
                    Background = UsageBarBrushes.GetBrush(TrackBackdropColor),
                    BorderBrush = UsageBarBrushes.GetBrush(TrackBorderColor),
                    BorderThickness = new Thickness(1)
                },
                fill
            }
        };

        return (track, fill);
    }

    public static void Apply(
        Grid track,
        Border fill,
        ref double lastPercent,
        double percentUsed,
        string detailLabel,
        bool showDetails,
        TextBlock? detailText = null)
    {
        track.Opacity = 1;
        lastPercent = percentUsed;
        fill.Background = CreateHatchBrush(UsageBarColors.GetColorForPercent(percentUsed));
        ProviderBarPresenter.UpdateProgressWidth(track, fill, lastPercent);

        if (detailText is not null)
        {
            detailText.Text = showDetails ? detailLabel : "";
            detailText.IsVisible = showDetails;
        }
    }
}

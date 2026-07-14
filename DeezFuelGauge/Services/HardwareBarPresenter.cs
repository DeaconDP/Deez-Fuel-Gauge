using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace DeezFuelGauge.Services;

public static class HardwareBarPresenter
{
    public const double TrackHeight = 6;
    public const double HatchStripeWidth = 4;

    private static readonly Color UnavailableColor = Color.FromRgb(0x55, 0x55, 0x55);
    private static readonly Color TrackBackdropColor = Color.FromArgb(0x1A, 0xFF, 0xFF, 0xFF);

    public static IBrush CreateDiagonalHatchBrush(Color baseColor)
    {
        var dark = Color.FromArgb(
            baseColor.A,
            (byte)(baseColor.R * 0.5),
            (byte)(baseColor.G * 0.5),
            (byte)(baseColor.B * 0.5));

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Absolute),
            EndPoint = new RelativePoint(HatchStripeWidth, HatchStripeWidth, RelativeUnit.Absolute),
            SpreadMethod = GradientSpreadMethod.Repeat,
            GradientStops =
            {
                new GradientStop(baseColor, 0),
                new GradientStop(baseColor, 0.42),
                new GradientStop(dark, 0.48),
                new GradientStop(dark, 0.58),
                new GradientStop(baseColor, 0.64),
                new GradientStop(baseColor, 1)
            }
        };
    }

    public static (Grid Track, Border Fill) CreateTrack()
    {
        var fill = new Border
        {
            CornerRadius = default,
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = 0,
            ClipToBounds = true,
            Background = CreateDiagonalHatchBrush(UsageBarColors.GetColorForPercent(0))
        };

        var track = new Grid
        {
            Height = TrackHeight,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new Border
                {
                    CornerRadius = default,
                    Background = UsageBarBrushes.GetBrush(TrackBackdropColor)
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
        bool isAvailable,
        string? statusMessage,
        string detailLabel,
        bool showDetails,
        TextBlock? detailText = null)
    {
        if (!isAvailable)
        {
            lastPercent = 0;
            fill.Width = 0;
            fill.Background = CreateDiagonalHatchBrush(UnavailableColor);
            track.Opacity = 0.45;

            if (detailText is not null)
            {
                var text = showDetails ? statusMessage ?? detailLabel ?? "—" : "";
                detailText.Text = text;
                detailText.IsVisible = showDetails && text.Length > 0;
            }

            return;
        }

        track.Opacity = 1;
        lastPercent = percentUsed;
        fill.Background = CreateDiagonalHatchBrush(UsageBarColors.GetColorForPercent(percentUsed));
        ProviderBarPresenter.UpdateProgressWidth(track, fill, lastPercent);

        if (detailText is not null)
        {
            detailText.Text = showDetails ? detailLabel : "";
            detailText.IsVisible = showDetails;
        }
    }
}

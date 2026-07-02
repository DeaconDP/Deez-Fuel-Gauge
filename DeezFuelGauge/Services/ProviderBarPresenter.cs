using Avalonia.Controls;
using Avalonia.Media;

namespace DeezFuelGauge.Services;

public static class ProviderBarPresenter
{
    public const double ReadySliverMinWidth = 3;

    private static readonly Color UnavailableColor = Color.FromRgb(0x55, 0x55, 0x55);
    private static readonly BoxShadows ReadySliverGlow = new(new BoxShadow
    {
        Blur = 6,
        Color = Color.FromArgb(160, 0x4D, 0x9F, 0xFF)
    });

    public static void ApplyUsageBar(
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
            fill.Background = new SolidColorBrush(UnavailableColor);
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
        fill.Background = new SolidColorBrush(UsageBarColors.GetColorForPercent(percentUsed));
        UpdateProgressWidth(track, fill, lastPercent);
        if (detailText is not null)
        {
            detailText.Text = showDetails ? detailLabel : "";
            detailText.IsVisible = showDetails;
        }
    }

    public static double ComputeFillWidth(double trackWidth, double percentUsed, bool showReadySliver)
    {
        if (trackWidth <= 0)
            return 0;

        var width = trackWidth * (percentUsed / 100.0);
        if (showReadySliver && percentUsed <= 0)
            return Math.Max(width, ReadySliverMinWidth);

        return width;
    }

    public static void SetReadySliverState(Border fill, bool showReadySliver) =>
        fill.Tag = showReadySliver;

    public static void UpdateProgressWidth(Grid track, Border fill, double percentUsed)
    {
        var showReadySliver = fill.Tag is true;
        var trackWidth = track.Bounds.Width;
        if (trackWidth <= 0)
            return;

        fill.Width = ComputeFillWidth(trackWidth, percentUsed, showReadySliver);
        ApplyReadyGlow(fill, showReadySliver && percentUsed <= 0);
    }

    public static void ApplyReadyGlow(Border fill, bool active) =>
        fill.BoxShadow = active ? ReadySliverGlow : default;
}

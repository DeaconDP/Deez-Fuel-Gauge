using Avalonia.Controls;
using Avalonia.Media;

namespace CursorUsageWidget.Services;

public static class ProviderBarPresenter
{
    private static readonly Color UnavailableColor = Color.FromRgb(0x55, 0x55, 0x55);

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

    public static void UpdateProgressWidth(Grid track, Border fill, double percentUsed)
    {
        var trackWidth = track.Bounds.Width;
        if (trackWidth <= 0)
            return;

        fill.Width = trackWidth * (percentUsed / 100.0);
    }
}

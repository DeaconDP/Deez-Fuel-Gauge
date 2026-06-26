using System.Globalization;

namespace CursorUsageWidget.Models;

public sealed class DiskVolumeSnapshot
{
    public string Name { get; init; } = "";
    public string DisplayLabel { get; init; } = "";
    public double PercentUsed { get; init; }
    public string DetailLabel { get; init; } = "";

    internal static double CalculatePercentUsed(long totalBytes, long freeBytes)
    {
        if (totalBytes <= 0)
            return 0;

        var usedBytes = totalBytes - freeBytes;
        var percent = usedBytes * 100.0 / totalBytes;
        return Math.Clamp(percent, 0, 100);
    }

    internal static string FormatDetailLabel(long totalBytes, long freeBytes)
    {
        var freeGb = FormatGigabytes(freeBytes);
        var totalGb = FormatGigabytes(totalBytes);
        return $"{freeGb} free of {totalGb}";
    }

    internal static string FormatGigabytes(long bytes)
    {
        var gb = bytes / 1_073_741_824.0;
        return $"{gb.ToString("F1", CultureInfo.InvariantCulture)} GB";
    }

    internal static string BuildDisplayLabel(string driveName, string volumeLabel, bool isWindows)
    {
        if (isWindows)
        {
            var label = driveName.TrimEnd('\\', '/');
            if (!string.IsNullOrWhiteSpace(volumeLabel))
                return $"{label} ({volumeLabel})";
            return label;
        }

        if (!string.IsNullOrWhiteSpace(volumeLabel))
            return volumeLabel;
        return driveName;
    }
}

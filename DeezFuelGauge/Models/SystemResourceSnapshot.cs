using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class SystemResourceSnapshot
{
    public const string RamName = "ram";
    public const string CpuName = "cpu";
    public const string GpuName = "gpu";

    public string Name { get; init; } = "";
    public string DisplayLabel { get; init; } = "";
    public double PercentUsed { get; init; }
    public string DetailLabel { get; init; } = "";
    public bool IsAvailable { get; init; } = true;
    public string? StatusMessage { get; init; }

    internal static double CalculatePercentUsed(long totalBytes, long freeBytes)
    {
        if (totalBytes <= 0)
            return 0;

        var usedBytes = totalBytes - freeBytes;
        var percent = usedBytes * 100.0 / totalBytes;
        return Math.Clamp(percent, 0, 100);
    }

    internal static double ClampPercent(double percent) => Math.Clamp(percent, 0, 100);

    internal static string FormatMemoryDetailLabel(long totalBytes, long freeBytes)
    {
        var freeGb = FormatGigabytes(freeBytes);
        var totalGb = FormatGigabytes(totalBytes);
        return $"{freeGb} free of {totalGb}";
    }

    internal static string FormatCpuDetailLabel(double percentUsed, int processorCount) =>
        $"{percentUsed.ToString("F0", CultureInfo.InvariantCulture)}% across {processorCount} cores";

    internal static string FormatGpuDetailLabel(double percentUsed) =>
        $"{percentUsed.ToString("F0", CultureInfo.InvariantCulture)}% utilization";

    internal static string FormatGigabytes(long bytes)
    {
        var gb = bytes / 1_073_741_824.0;
        return $"{gb.ToString("F1", CultureInfo.InvariantCulture)} GB";
    }
}

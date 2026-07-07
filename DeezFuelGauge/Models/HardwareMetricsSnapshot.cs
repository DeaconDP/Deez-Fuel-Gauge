using System.Globalization;

namespace DeezFuelGauge.Models;

public sealed class HardwareMetricsSnapshot
{
    public double? CpuPercent { get; init; }
    public double? GpuPercent { get; init; }
    public double RamPercent { get; init; }
    public double? CpuTempCelsius { get; init; }
    public long RamUsedBytes { get; init; }
    public long RamTotalBytes { get; init; }
    public bool IsGpuAvailable { get; init; }
    public bool IsCpuTempAvailable { get; init; }

    public static string FormatRamDetail(long usedBytes, long totalBytes)
    {
        var usedGb = FormatGigabytes(usedBytes);
        var totalGb = FormatGigabytes(totalBytes);
        return $"{usedGb} / {totalGb}";
    }

    public static string FormatCpuTemp(double? celsius)
    {
        if (celsius is not { } value)
            return "—";

        return $"{value.ToString("F0", CultureInfo.InvariantCulture)}°C";
    }

    internal static double ClampPercent(double percent) => Math.Clamp(percent, 0, 100);

    internal static double CalculateRamPercent(long usedBytes, long totalBytes)
    {
        if (totalBytes <= 0)
            return 0;

        return ClampPercent(usedBytes * 100.0 / totalBytes);
    }

    internal static double? CalculateCpuPercent(
        ulong idle1,
        ulong kernel1,
        ulong user1,
        ulong idle2,
        ulong kernel2,
        ulong user2)
    {
        var idleDelta = idle2 - idle1;
        var kernelDelta = kernel2 - kernel1;
        var userDelta = user2 - user1;
        var total = kernelDelta + userDelta;
        if (total == 0)
            return null;

        var busy = total - idleDelta;
        return ClampPercent(busy * 100.0 / total);
    }

    internal static double? ConvertThermalCounterValue(double rawValue, bool isHighPrecision = false)
    {
        if (rawValue <= 0 || double.IsNaN(rawValue) || double.IsInfinity(rawValue))
            return null;

        double celsius;
        if (isHighPrecision || rawValue >= 1000)
            celsius = (rawValue / 10.0) - 273.15;
        else
            celsius = rawValue - 273.15;

        if (celsius is < 5 or > 115)
            return null;

        return Math.Round(celsius, 1);
    }

    private static string FormatGigabytes(long bytes)
    {
        var gb = bytes / 1_073_741_824.0;
        return $"{gb.ToString("F1", CultureInfo.InvariantCulture)} GB";
    }
}

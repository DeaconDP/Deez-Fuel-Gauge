using System.Runtime.InteropServices;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services.Platform;

namespace DeezFuelGauge.Services;

public static class SystemResourceProvider
{
    private static ulong _lastCpuIdle;
    private static ulong _lastCpuTotal;
    private static bool _hasCpuBaseline;
    private static double _lastCpuPercent;

    public static IReadOnlyList<SystemResourceSnapshot> GetMetrics(WidgetSettings settings)
    {
        if (!settings.ShowSystemResources)
            return [];

        var metrics = new List<SystemResourceSnapshot>(3);

        if (settings.ShowRam)
            metrics.Add(ReadRam());

        if (settings.ShowCpu)
            metrics.Add(ReadCpu());

        if (settings.ShowGpu)
            metrics.Add(ReadGpu());

        return metrics;
    }

    internal static double ComputeCpuPercent(ulong idleTicks, ulong totalTicks)
    {
        if (!_hasCpuBaseline)
        {
            _lastCpuIdle = idleTicks;
            _lastCpuTotal = totalTicks;
            _hasCpuBaseline = true;
            return _lastCpuPercent;
        }

        var idleDelta = idleTicks - _lastCpuIdle;
        var totalDelta = totalTicks - _lastCpuTotal;
        _lastCpuIdle = idleTicks;
        _lastCpuTotal = totalTicks;

        if (totalDelta == 0)
            return _lastCpuPercent;

        var used = (totalDelta - idleDelta) * 100.0 / totalDelta;
        _lastCpuPercent = SystemResourceSnapshot.ClampPercent(used);
        return _lastCpuPercent;
    }

    internal static void ResetCpuBaselineForTests()
    {
        _hasCpuBaseline = false;
        _lastCpuPercent = 0;
        _lastCpuIdle = 0;
        _lastCpuTotal = 0;
    }

    private static SystemResourceSnapshot ReadRam()
    {
        if (!TryGetMemory(out var totalBytes, out var freeBytes) || totalBytes <= 0)
        {
            return new SystemResourceSnapshot
            {
                Name = SystemResourceSnapshot.RamName,
                DisplayLabel = "RAM",
                IsAvailable = false,
                StatusMessage = "RAM usage unavailable"
            };
        }

        return new SystemResourceSnapshot
        {
            Name = SystemResourceSnapshot.RamName,
            DisplayLabel = "RAM",
            PercentUsed = SystemResourceSnapshot.CalculatePercentUsed(totalBytes, freeBytes),
            DetailLabel = SystemResourceSnapshot.FormatMemoryDetailLabel(totalBytes, freeBytes),
            IsAvailable = true
        };
    }

    private static SystemResourceSnapshot ReadCpu()
    {
        if (!TryGetCpuTimes(out var idleTicks, out var totalTicks))
        {
            return new SystemResourceSnapshot
            {
                Name = SystemResourceSnapshot.CpuName,
                DisplayLabel = "CPU",
                IsAvailable = false,
                StatusMessage = "CPU usage unavailable"
            };
        }

        var percent = ComputeCpuPercent(idleTicks, totalTicks);
        return new SystemResourceSnapshot
        {
            Name = SystemResourceSnapshot.CpuName,
            DisplayLabel = "CPU",
            PercentUsed = percent,
            DetailLabel = SystemResourceSnapshot.FormatCpuDetailLabel(percent, Environment.ProcessorCount),
            IsAvailable = true
        };
    }

    private static SystemResourceSnapshot ReadGpu()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return new SystemResourceSnapshot
            {
                Name = SystemResourceSnapshot.GpuName,
                DisplayLabel = "GPU",
                IsAvailable = false,
                StatusMessage = "Not available on macOS",
                DetailLabel = "Not available on macOS"
            };
        }

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new SystemResourceSnapshot
            {
                Name = SystemResourceSnapshot.GpuName,
                DisplayLabel = "GPU",
                IsAvailable = false,
                StatusMessage = "GPU usage unavailable"
            };
        }

        if (!WindowsGpuMetrics.TryGetUtilization(out var percentUsed))
        {
            return new SystemResourceSnapshot
            {
                Name = SystemResourceSnapshot.GpuName,
                DisplayLabel = "GPU",
                IsAvailable = false,
                StatusMessage = "GPU usage unavailable"
            };
        }

        return new SystemResourceSnapshot
        {
            Name = SystemResourceSnapshot.GpuName,
            DisplayLabel = "GPU",
            PercentUsed = percentUsed,
            DetailLabel = SystemResourceSnapshot.FormatGpuDetailLabel(percentUsed),
            IsAvailable = true
        };
    }

    private static bool TryGetMemory(out long totalBytes, out long freeBytes)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsSystemMetrics.TryGetMemory(out totalBytes, out freeBytes);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacOsSystemMetrics.TryGetMemory(out totalBytes, out freeBytes);

        totalBytes = 0;
        freeBytes = 0;
        return false;
    }

    private static bool TryGetCpuTimes(out ulong idleTicks, out ulong totalTicks)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return WindowsSystemMetrics.TryGetCpuTimes(out idleTicks, out totalTicks);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return MacOsSystemMetrics.TryGetCpuTimes(out idleTicks, out totalTicks);

        idleTicks = 0;
        totalTicks = 0;
        return false;
    }
}

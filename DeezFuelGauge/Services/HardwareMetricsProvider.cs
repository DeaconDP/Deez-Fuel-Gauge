using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services.Hardware;

namespace DeezFuelGauge.Services;

public sealed class HardwareMetricsProvider : IDisposable
{
    private readonly WindowsHardwareReader? _windowsReader;
    private readonly MacOsHardwareReader? _macReader;
    private ulong _lastIdle;
    private ulong _lastKernel;
    private ulong _lastUser;
    private bool _hasCpuBaseline;
    private bool _disposed;

    public HardwareMetricsProvider()
    {
        if (OperatingSystem.IsWindows())
            _windowsReader = new WindowsHardwareReader();
        else if (OperatingSystem.IsMacOS())
            _macReader = new MacOsHardwareReader();
    }

    public HardwareMetricsSnapshot Sample()
    {
        if (OperatingSystem.IsWindows() && _windowsReader is not null)
            return SampleWindows(_windowsReader);

        if (OperatingSystem.IsMacOS() && _macReader is not null)
            return SampleMacOs(_macReader);

        return new HardwareMetricsSnapshot();
    }

    public void ResetCpuBaseline()
    {
        _hasCpuBaseline = false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (OperatingSystem.IsWindows())
            _windowsReader?.Dispose();
    }

    [SupportedOSPlatform("windows")]
    private HardwareMetricsSnapshot SampleWindows(WindowsHardwareReader reader)
    {
        var (idle, kernel, user) = reader.ReadCpuTimes();
        var cpuPercent = ReadCpuPercent(idle, kernel, user);
        var (ramPercent, ramUsed, ramTotal) = reader.ReadRam();

        // GPU / thermal perf-counter init can be slow or fail on some machines.
        // Never let that block CPU/RAM from reaching the UI.
        double? gpuPercent = null;
        var gpuAvailable = false;
        try
        {
            gpuPercent = reader.ReadGpuPercent();
            gpuAvailable = reader.HasGpuSupport;
        }
        catch
        {
            // Keep CPU/RAM readings.
        }

        double? cpuTemp = null;
        try
        {
            cpuTemp = reader.ReadCpuTempCelsius();
        }
        catch
        {
            // Optional metric.
        }

        return new HardwareMetricsSnapshot
        {
            CpuPercent = cpuPercent,
            GpuPercent = gpuPercent,
            RamPercent = ramPercent,
            RamUsedBytes = ramUsed,
            RamTotalBytes = ramTotal,
            IsGpuAvailable = gpuAvailable,
            IsCpuTempAvailable = cpuTemp is not null,
            CpuTempCelsius = cpuTemp
        };
    }

    private HardwareMetricsSnapshot SampleMacOs(MacOsHardwareReader reader)
    {
        var (idle, kernel, user) = reader.ReadCpuTimes();
        var cpuPercent = ReadCpuPercent(idle, kernel, user);
        var (ramPercent, ramUsed, ramTotal) = reader.ReadRam();

        return new HardwareMetricsSnapshot
        {
            CpuPercent = cpuPercent,
            GpuPercent = null,
            RamPercent = ramPercent,
            RamUsedBytes = ramUsed,
            RamTotalBytes = ramTotal,
            IsGpuAvailable = false,
            IsCpuTempAvailable = false
        };
    }

    private double? ReadCpuPercent(ulong idle, ulong kernel, ulong user)
    {
        if (!_hasCpuBaseline)
        {
            _lastIdle = idle;
            _lastKernel = kernel;
            _lastUser = user;
            _hasCpuBaseline = true;
            return null;
        }

        var percent = HardwareMetricsSnapshot.CalculateCpuPercent(
            _lastIdle,
            _lastKernel,
            _lastUser,
            idle,
            kernel,
            user);

        _lastIdle = idle;
        _lastKernel = kernel;
        _lastUser = user;
        return percent;
    }
}

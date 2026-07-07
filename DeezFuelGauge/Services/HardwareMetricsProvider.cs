using System.Runtime.InteropServices;
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
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            _windowsReader = new WindowsHardwareReader();
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            _macReader = new MacOsHardwareReader();
    }

    public HardwareMetricsSnapshot Sample()
    {
        if (_windowsReader is not null)
            return SampleWindows(_windowsReader);

        if (_macReader is not null)
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
        _windowsReader?.Dispose();
    }

    private HardwareMetricsSnapshot SampleWindows(WindowsHardwareReader reader)
    {
        var (idle, kernel, user) = reader.ReadCpuTimes();
        var cpuPercent = ReadCpuPercent(idle, kernel, user);
        var (ramPercent, ramUsed, ramTotal) = reader.ReadRam();
        var gpuPercent = reader.ReadGpuPercent();
        var cpuTemp = reader.ReadCpuTempCelsius();

        return new HardwareMetricsSnapshot
        {
            CpuPercent = cpuPercent,
            GpuPercent = gpuPercent,
            RamPercent = ramPercent,
            RamUsedBytes = ramUsed,
            RamTotalBytes = ramTotal,
            IsGpuAvailable = reader.HasGpuSupport,
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

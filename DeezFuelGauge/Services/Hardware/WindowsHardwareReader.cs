using System.Diagnostics;
using System.Runtime.InteropServices;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services.Hardware;

internal sealed class WindowsHardwareReader : IDisposable
{
    private const string HighPrecisionTemperatureCounter = "High Precision Temperature";
    private const string TemperatureCounter = "Temperature";

    private readonly List<ThermalCounter> _thermalCounters = [];
    private readonly List<PerformanceCounter> _gpuCounters = [];
    private bool _gpuCountersInitialized;
    private bool _thermalCountersInitialized;
    private bool _gpuWarmedUp;
    private bool _disposed;

    public bool HasGpuSupport
    {
        get
        {
            EnsureGpuCounters();
            return _gpuCounters.Count > 0;
        }
    }

    public (ulong Idle, ulong Kernel, ulong User) ReadCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
            return (0, 0, 0);

        return (idle, kernel, user);
    }

    public (double Percent, long UsedBytes, long TotalBytes) ReadRam()
    {
        var status = new MemoryStatusEx { DwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.UllTotalPhys == 0)
            return (0, 0, 0);

        var total = (long)status.UllTotalPhys;
        var available = (long)status.UllAvailPhys;
        var used = total - available;
        var percent = HardwareMetricsSnapshot.CalculateRamPercent(used, total);
        return (percent, used, total);
    }

    public double? ReadGpuPercent()
    {
        EnsureGpuCounters();
        if (_gpuCounters.Count == 0)
            return null;

        if (!_gpuWarmedUp)
        {
            foreach (var counter in _gpuCounters)
                _ = counter.NextValue();

            _gpuWarmedUp = true;
            return null;
        }

        var total = 0.0;
        foreach (var counter in _gpuCounters)
            total += counter.NextValue();

        return HardwareMetricsSnapshot.ClampPercent(total);
    }

    public double? ReadCpuTempCelsius()
    {
        EnsureThermalCounters();
        if (_thermalCounters.Count == 0)
            return null;

        var readings = new List<double>();
        foreach (var thermal in _thermalCounters)
        {
            var converted = HardwareMetricsSnapshot.ConvertThermalCounterValue(
                thermal.Counter.NextValue(),
                thermal.IsHighPrecision);
            if (converted is not null)
                readings.Add(converted.Value);
        }

        if (readings.Count == 0)
            return null;

        return Math.Round(readings.Average(), 1);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeCounters(_gpuCounters);
        foreach (var thermal in _thermalCounters)
            thermal.Counter.Dispose();

        _thermalCounters.Clear();
    }

    private void EnsureGpuCounters()
    {
        if (_gpuCountersInitialized)
            return;

        _gpuCountersInitialized = true;
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return;

            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames())
            {
                if (!instance.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach (var counter in category.GetCounters(instance))
                {
                    if (!string.Equals(counter.CounterName, "Utilization Percentage", StringComparison.Ordinal))
                        continue;

                    _gpuCounters.Add(counter);
                }
            }
        }
        catch
        {
            _gpuCounters.Clear();
        }
    }

    private void EnsureThermalCounters()
    {
        if (_thermalCountersInitialized)
            return;

        _thermalCountersInitialized = true;
        try
        {
            if (!PerformanceCounterCategory.Exists("Thermal Zone Information"))
                return;

            var category = new PerformanceCounterCategory("Thermal Zone Information");
            foreach (var instance in category.GetInstanceNames())
            {
                PerformanceCounter? highPrecision = null;
                PerformanceCounter? temperature = null;

                foreach (var counter in category.GetCounters(instance))
                {
                    if (string.Equals(counter.CounterName, HighPrecisionTemperatureCounter, StringComparison.Ordinal))
                        highPrecision = counter;
                    else if (string.Equals(counter.CounterName, TemperatureCounter, StringComparison.Ordinal))
                        temperature = counter;
                }

                if (highPrecision is not null)
                {
                    _thermalCounters.Add(new ThermalCounter(highPrecision, IsHighPrecision: true));
                    temperature?.Dispose();
                    continue;
                }

                if (temperature is not null)
                    _thermalCounters.Add(new ThermalCounter(temperature, IsHighPrecision: false));
            }
        }
        catch
        {
            foreach (var thermal in _thermalCounters)
                thermal.Counter.Dispose();

            _thermalCounters.Clear();
        }
    }

    private static void DisposeCounters(List<PerformanceCounter> counters)
    {
        foreach (var counter in counters)
            counter.Dispose();

        counters.Clear();
    }

    private sealed record ThermalCounter(PerformanceCounter Counter, bool IsHighPrecision);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out ulong lpIdleTime,
        out ulong lpKernelTime,
        out ulong lpUserTime);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint DwLength;
        public uint DwMemoryLoad;
        public ulong UllTotalPhys;
        public ulong UllAvailPhys;
        public ulong UllTotalPageFile;
        public ulong UllAvailPageFile;
        public ulong UllTotalVirtual;
        public ulong UllAvailVirtual;
        public ulong UllAvailExtendedVirtual;
    }
}

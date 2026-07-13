using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services.Hardware;

[SupportedOSPlatform("windows")]
internal sealed class WindowsHardwareReader : IDisposable
{
    private const string HighPrecisionTemperatureCounter = "High Precision Temperature";
    private const string TemperatureCounter = "Temperature";

    // GPU Engine exposes one instance per process engtype; machines with many GPU clients
    // can have hundreds. Cap + direct counter construction keeps Sample() from hanging.
    internal const int MaxGpuEngineCounters = 48;

    private readonly List<ThermalCounter> _thermalCounters = [];
    private readonly List<PerformanceCounter> _gpuCounters = [];
    private readonly object _gpuInitGate = new();
    private readonly object _thermalInitGate = new();
    private Task? _gpuInitTask;
    private Task? _thermalInitTask;
    private bool _gpuCountersInitialized;
    private bool _thermalCountersInitialized;
    private bool _gpuWarmedUp;
    private bool _disposed;

    public bool HasGpuSupport => _gpuCountersInitialized && _gpuCounters.Count > 0;

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
        EnsureGpuCountersAsync();
        if (!_gpuCountersInitialized || _gpuCounters.Count == 0)
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
        EnsureThermalCountersAsync();
        if (!_thermalCountersInitialized || _thermalCounters.Count == 0)
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
        try
        {
            _gpuInitTask?.Wait(TimeSpan.FromSeconds(2));
            _thermalInitTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Best-effort shutdown if background init is still running.
        }

        lock (_gpuInitGate)
            DisposeCounters(_gpuCounters);

        lock (_thermalInitGate)
        {
            foreach (var thermal in _thermalCounters)
                thermal.Counter.Dispose();

            _thermalCounters.Clear();
        }
    }

    internal static IReadOnlyList<string> SelectGpuEngineInstances(
        IEnumerable<string> instanceNames,
        int maxCount = MaxGpuEngineCounters)
    {
        if (maxCount <= 0)
            return [];

        return instanceNames
            .Where(name => name.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase))
            .Take(maxCount)
            .ToArray();
    }

    private void EnsureGpuCountersAsync()
    {
        if (_gpuCountersInitialized || _gpuInitTask is not null)
            return;

        lock (_gpuInitGate)
        {
            if (_gpuCountersInitialized || _gpuInitTask is not null)
                return;

            _gpuInitTask = Task.Run(InitializeGpuCounters);
        }
    }

    private void InitializeGpuCounters()
    {
        try
        {
            if (!PerformanceCounterCategory.Exists("GPU Engine"))
                return;

            var category = new PerformanceCounterCategory("GPU Engine");
            var opened = new List<PerformanceCounter>();
            foreach (var instance in SelectGpuEngineInstances(category.GetInstanceNames()))
            {
                try
                {
                    // Construct the one counter we need — GetCounters(instance) enumerates every
                    // counter for that instance and is extremely slow with hundreds of engines.
                    opened.Add(new PerformanceCounter(
                        "GPU Engine",
                        "Utilization Percentage",
                        instance,
                        readOnly: true));
                }
                catch
                {
                    // Skip instances that disappear between enumeration and open.
                }
            }

            lock (_gpuInitGate)
            {
                if (_disposed)
                {
                    DisposeCounters(opened);
                    return;
                }

                _gpuCounters.AddRange(opened);
            }
        }
        catch
        {
            lock (_gpuInitGate)
                DisposeCounters(_gpuCounters);
        }
        finally
        {
            lock (_gpuInitGate)
                _gpuCountersInitialized = true;
        }
    }

    private void EnsureThermalCountersAsync()
    {
        if (_thermalCountersInitialized || _thermalInitTask is not null)
            return;

        lock (_thermalInitGate)
        {
            if (_thermalCountersInitialized || _thermalInitTask is not null)
                return;

            _thermalInitTask = Task.Run(InitializeThermalCounters);
        }
    }

    private void InitializeThermalCounters()
    {
        var opened = new List<ThermalCounter>();
        try
        {
            if (!PerformanceCounterCategory.Exists("Thermal Zone Information"))
                return;

            var category = new PerformanceCounterCategory("Thermal Zone Information");
            foreach (var instance in category.GetInstanceNames())
            {
                PerformanceCounter? highPrecision = null;
                try
                {
                    highPrecision = new PerformanceCounter(
                        "Thermal Zone Information",
                        HighPrecisionTemperatureCounter,
                        instance,
                        readOnly: true);
                }
                catch
                {
                    // Not all zones expose the high-precision counter.
                }

                if (highPrecision is not null)
                {
                    opened.Add(new ThermalCounter(highPrecision, IsHighPrecision: true));
                    continue;
                }

                try
                {
                    var temperature = new PerformanceCounter(
                        "Thermal Zone Information",
                        TemperatureCounter,
                        instance,
                        readOnly: true);
                    opened.Add(new ThermalCounter(temperature, IsHighPrecision: false));
                }
                catch
                {
                    // Skip zones without a usable temperature counter.
                }
            }

            lock (_thermalInitGate)
            {
                if (_disposed)
                {
                    foreach (var thermal in opened)
                        thermal.Counter.Dispose();
                    return;
                }

                _thermalCounters.AddRange(opened);
            }
        }
        catch
        {
            foreach (var thermal in opened)
                thermal.Counter.Dispose();

            lock (_thermalInitGate)
                _thermalCounters.Clear();
        }
        finally
        {
            lock (_thermalInitGate)
                _thermalCountersInitialized = true;
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

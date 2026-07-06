using System.Diagnostics;
using System.Runtime.InteropServices;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services.Platform;

internal static class WindowsGpuMetrics
{
    public static bool TryGetUtilization(out double percentUsed)
    {
        percentUsed = 0;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            var instances = category.GetInstanceNames();
            if (instances.Length == 0)
                return false;

            var max = 0.0;
            foreach (var instance in instances)
            {
                if (!instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var counter = new PerformanceCounter("GPU Engine", "Utilization Percentage", instance, readOnly: true);
                _ = counter.NextValue();
                max = Math.Max(max, counter.NextValue());
            }

            if (max <= 0)
                return false;

            percentUsed = SystemResourceSnapshot.ClampPercent(max);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

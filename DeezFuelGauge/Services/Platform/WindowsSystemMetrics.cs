using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services.Platform;

internal static class WindowsSystemMetrics
{
    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;

        public ulong ToUInt64() => ((ulong)HighDateTime << 32) | LowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

    public static bool TryGetMemory(out long totalBytes, out long freeBytes)
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0)
        {
            totalBytes = 0;
            freeBytes = 0;
            return false;
        }

        totalBytes = (long)status.TotalPhys;
        freeBytes = (long)status.AvailPhys;
        return true;
    }

    public static bool TryGetCpuTimes(out ulong idleTicks, out ulong totalTicks)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            idleTicks = 0;
            totalTicks = 0;
            return false;
        }

        idleTicks = idle.ToUInt64();
        totalTicks = kernel.ToUInt64() + user.ToUInt64();
        return totalTicks > 0;
    }
}

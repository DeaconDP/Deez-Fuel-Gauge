using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services.Platform;

internal static class MacOsSystemMetrics
{
    private const int CtlHw = 6;
    private const int HwMemsize = 24;
    private const int HwPagesize = 7;
    private const int HostVmInfo64 = 4;
    private const int HostCpuLoadInfo = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct VmStatistics64
    {
        public uint FreeCount;
        public uint ActiveCount;
        public uint InactiveCount;
        public uint WireCount;
        public ulong ZeroFillCount;
        public ulong Reactivations;
        public ulong Pageins;
        public ulong Pageouts;
        public ulong Faults;
        public ulong CowFaults;
        public ulong Lookups;
        public ulong Hits;
        public ulong Purges;
        public uint PurgeableCount;
        public uint SpeculativeCount;
        public ulong Decompressions;
        public ulong Compressions;
        public ulong Swapins;
        public ulong Swapouts;
        public uint CompressorPageCount;
        public uint ThrottledCount;
        public uint ExternalPageCount;
        public uint InternalPageCount;
        public ulong TotalUncompressedPagesInCompressor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HostCpuLoadInfoData
    {
        public uint User;
        public uint System;
        public uint Idle;
        public uint Nice;
    }

    [DllImport("libc")]
    private static extern int sysctl(int[] name, uint namelen, byte[] oldp, ref nuint oldlenp, IntPtr newp, nuint newlen);

    [DllImport("libc")]
    private static extern IntPtr mach_host_self();

    [DllImport("libc")]
    private static extern int host_statistics64(
        IntPtr host,
        int flavor,
        IntPtr info,
        ref int count);

    [DllImport("libc")]
    private static extern int host_processor_info(
        IntPtr host,
        int flavor,
        ref int natural,
        out IntPtr info,
        ref int count);

    [DllImport("libc")]
    private static extern int vm_deallocate(IntPtr targetTask, IntPtr address, nuint size);

    public static bool TryGetMemory(out long totalBytes, out long freeBytes)
    {
        totalBytes = 0;
        freeBytes = 0;

        if (!TryReadSysctlLong([CtlHw, HwMemsize], out totalBytes) || totalBytes <= 0)
            return false;

        if (!TryReadSysctlInt([CtlHw, HwPagesize], out var pageSize) || pageSize <= 0)
            return false;

        var stats = new VmStatistics64();
        var size = Marshal.SizeOf<VmStatistics64>();
        var infoPtr = Marshal.AllocHGlobal(size);
        try
        {
            var count = size / sizeof(int);
            if (host_statistics64(mach_host_self(), HostVmInfo64, infoPtr, ref count) != 0)
                return false;

            stats = Marshal.PtrToStructure<VmStatistics64>(infoPtr);
        }
        finally
        {
            Marshal.FreeHGlobal(infoPtr);
        }

        var usedPages = (long)stats.ActiveCount + stats.WireCount + stats.CompressorPageCount;
        var freePages = (long)stats.FreeCount;
        var usedBytes = usedPages * pageSize;
        freeBytes = Math.Max(0, totalBytes - usedBytes);
        if (freePages > 0)
            freeBytes = Math.Min(freeBytes, freePages * pageSize);

        return true;
    }

    public static bool TryGetCpuTimes(out ulong idleTicks, out ulong totalTicks)
    {
        idleTicks = 0;
        totalTicks = 0;

        var natural = 0;
        var count = 0;
        if (host_processor_info(mach_host_self(), HostCpuLoadInfo, ref natural, out var info, ref count) != 0 || count <= 0)
            return false;

        try
        {
            var stride = Marshal.SizeOf<HostCpuLoadInfoData>();
            ulong idle = 0;
            ulong total = 0;

            for (var i = 0; i < count; i++)
            {
                var load = Marshal.PtrToStructure<HostCpuLoadInfoData>(info + (i * stride));
                idle += load.Idle;
                total += load.User + load.System + load.Idle + load.Nice;
            }

            idleTicks = idle;
            totalTicks = total;
            return totalTicks > 0;
        }
        finally
        {
            vm_deallocate(mach_host_self(), info, (nuint)(count * Marshal.SizeOf<HostCpuLoadInfoData>()));
        }
    }

    private static bool TryReadSysctlLong(int[] mib, out long value)
    {
        value = 0;
        nuint length = (nuint)sizeof(long);
        var buffer = new byte[sizeof(long)];
        if (sysctl(mib, (uint)mib.Length, buffer, ref length, IntPtr.Zero, 0) != 0)
            return false;

        value = BitConverter.ToInt64(buffer, 0);
        return true;
    }

    private static bool TryReadSysctlInt(int[] mib, out int value)
    {
        value = 0;
        nuint length = (nuint)sizeof(int);
        var buffer = new byte[sizeof(int)];
        if (sysctl(mib, (uint)mib.Length, buffer, ref length, IntPtr.Zero, 0) != 0)
            return false;

        value = BitConverter.ToInt32(buffer, 0);
        return true;
    }
}

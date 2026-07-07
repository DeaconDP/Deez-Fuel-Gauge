using System.Runtime.InteropServices;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services.Hardware;

internal sealed class MacOsHardwareReader
{
    private const int HostCpuLoadInfo = 3;
    private const int HostVmInfo64 = 4;
    private const int HostCpuLoadInfoCount = 4;
    private static readonly IntPtr MachHostSelf = new(-1);

    public (ulong Idle, ulong Kernel, ulong User) ReadCpuTimes()
    {
        var processorCount = 0;
        var processorInfo = IntPtr.Zero;
        var processorMessageCount = 0;
        if (host_processor_info(
                MachHostSelf,
                HostCpuLoadInfo,
                ref processorCount,
                ref processorInfo,
                ref processorMessageCount) != 0
            || processorInfo == IntPtr.Zero
            || processorMessageCount <= 0)
        {
            return (0, 0, 0);
        }

        try
        {
            var cpuInfo = new int[processorMessageCount];
            Marshal.Copy(processorInfo, cpuInfo, 0, processorMessageCount);

            ulong user = 0;
            ulong system = 0;
            ulong idle = 0;
            for (var i = 0; i < processorCount; i++)
            {
                var offset = i * HostCpuLoadInfoCount;
                if (offset + 3 >= cpuInfo.Length)
                    break;

                user += (uint)cpuInfo[offset];
                system += (uint)cpuInfo[offset + 2];
                idle += (uint)cpuInfo[offset + 3];
            }

            return (idle, system, user);
        }
        finally
        {
            vm_deallocate(MachHostSelf, processorInfo, processorMessageCount * sizeof(int));
        }
    }

    public (double Percent, long UsedBytes, long TotalBytes) ReadRam()
    {
        var total = ReadTotalMemoryBytes();
        if (total <= 0)
            return (0, 0, 0);

        var pageSize = ReadPageSize();
        if (pageSize <= 0)
            return (0, 0, 0);

        var vmInfo = new VmStatistics64();
        var count = (uint)(Marshal.SizeOf<VmStatistics64>() / sizeof(int));
        if (host_statistics64(MachHostSelf, HostVmInfo64, ref vmInfo, ref count) != 0)
            return (0, 0, 0);

        var freePages = vmInfo.FreeCount + vmInfo.InactiveCount;
        var freeBytes = (long)freePages * pageSize;
        var usedBytes = total - freeBytes;
        if (usedBytes < 0)
            usedBytes = 0;

        var percent = HardwareMetricsSnapshot.CalculateRamPercent(usedBytes, total);
        return (percent, usedBytes, total);
    }

    private static long ReadTotalMemoryBytes()
    {
        var name = "hw.memsize";
        var length = (nuint)sizeof(long);
        var value = 0L;
        if (sysctlbyname(name, ref value, ref length, IntPtr.Zero, 0) != 0)
            return 0;

        return value;
    }

    private static long ReadPageSize()
    {
        var name = "hw.pagesize";
        var length = (nuint)sizeof(int);
        var value = 0;
        if (sysctlbyname(name, ref value, ref length, IntPtr.Zero, 0) != 0)
            return 0;

        return value;
    }

    [DllImport("libc")]
    private static extern int host_processor_info(
        IntPtr host,
        int flavor,
        ref int processorCount,
        ref IntPtr processorInfo,
        ref int processorMessageCount);

    [DllImport("libc")]
    private static extern int host_statistics64(
        IntPtr host,
        int flavor,
        ref VmStatistics64 hostInfo,
        ref uint count);

    [DllImport("libc")]
    private static extern int vm_deallocate(IntPtr target, IntPtr address, int size);

    [DllImport("libc")]
    private static extern int sysctlbyname(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ref long oldp,
        ref nuint oldlenp,
        IntPtr newp,
        nuint newlen);

    [DllImport("libc")]
    private static extern int sysctlbyname(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        ref int oldp,
        ref nuint oldlenp,
        IntPtr newp,
        nuint newlen);

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
}

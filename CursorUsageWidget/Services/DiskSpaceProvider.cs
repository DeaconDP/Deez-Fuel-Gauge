using System.Runtime.InteropServices;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public static class DiskSpaceProvider
{
    public static IReadOnlyList<DiskVolumeSnapshot> GetVolumes()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        return DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.TotalSize > 0)
            .Select(d => ToSnapshot(d, isWindows))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static DiskVolumeSnapshot ToSnapshot(DriveInfo drive, bool isWindows)
    {
        var totalBytes = drive.TotalSize;
        var freeBytes = drive.AvailableFreeSpace;

        return new DiskVolumeSnapshot
        {
            Name = drive.Name,
            DisplayLabel = DiskVolumeSnapshot.BuildDisplayLabel(drive.Name, drive.VolumeLabel, isWindows),
            PercentUsed = DiskVolumeSnapshot.CalculatePercentUsed(totalBytes, freeBytes),
            DetailLabel = DiskVolumeSnapshot.FormatDetailLabel(totalBytes, freeBytes)
        };
    }
}

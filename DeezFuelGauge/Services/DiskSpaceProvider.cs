using System.Runtime.InteropServices;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class DiskSpaceProvider
{
    private const string MacOsDataVolumePath = "/System/Volumes/Data";
    private const string MacOsRootVolumePath = "/";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static IReadOnlyList<DiskVolumeSnapshot>? _cachedVolumes;
    private static DateTimeOffset _cachedAt;

    public static IReadOnlyList<DiskVolumeSnapshot> GetVolumes()
    {
        if (_cachedVolumes is not null && DateTimeOffset.UtcNow - _cachedAt < CacheTtl)
            return _cachedVolumes;

        var volumes = QueryVolumes();
        _cachedVolumes = volumes;
        _cachedAt = DateTimeOffset.UtcNow;
        return volumes;
    }

    public static void InvalidateCache() => _cachedVolumes = null;

    private static IReadOnlyList<DiskVolumeSnapshot> QueryVolumes()
    {
        var drives = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.TotalSize > 0)
            .ToList();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return GetMacOsAggregatedVolumes(drives);

        return drives
            .Select(d => ToSnapshot(d, isWindows: true))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string? SelectMacOsPrimaryDriveName(IEnumerable<string> driveNames)
    {
        var names = driveNames.ToHashSet(StringComparer.Ordinal);

        if (names.Contains(MacOsDataVolumePath))
            return MacOsDataVolumePath;

        if (names.Contains(MacOsRootVolumePath))
            return MacOsRootVolumePath;

        return null;
    }

    internal static IReadOnlyList<DiskVolumeSnapshot> GetMacOsAggregatedVolumes(IEnumerable<DriveInfo> drives)
    {
        var driveList = drives.ToList();
        var primaryName = SelectMacOsPrimaryDriveName(driveList.Select(d => d.Name));
        if (primaryName is null)
            return [];

        var primaryDrive = driveList.FirstOrDefault(d =>
            string.Equals(d.Name, primaryName, StringComparison.Ordinal));
        if (primaryDrive is null)
            return [];

        return [ToSnapshot(primaryDrive, isWindows: false)];
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

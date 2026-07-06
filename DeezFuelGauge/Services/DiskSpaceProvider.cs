using System.Runtime.InteropServices;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class DiskSpaceProvider
{
    internal const string AggregatedVolumeName = "__aggregate__";

    private const string MacOsDataVolumePath = "/System/Volumes/Data";
    private const string MacOsRootVolumePath = "/";
    private const string AggregatedDisplayLabel = "All drives";

    public static IReadOnlyList<DiskVolumeSnapshot> GetVolumes(WidgetSettings settings)
    {
        var enabledDrives = GetEnabledDrives(settings);

        if (settings.DiskAggregateVolumes && enabledDrives.Count > 0)
            return [CreateAggregatedSnapshot(enabledDrives)];

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return enabledDrives
            .Select(d => ToSnapshot(d, isWindows))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<DriveInfo> GetReadyDrives() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.TotalSize > 0)
            .ToList();

    public static IReadOnlyList<DiskDriveDescriptor> GetDriveDescriptors()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        return GetReadyDrives()
            .Select(d => new DiskDriveDescriptor(
                d.Name,
                DiskVolumeSnapshot.BuildDisplayLabel(d.Name, d.VolumeLabel, isWindows)))
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static IReadOnlyList<DriveInfo> GetEnabledDrives(WidgetSettings settings)
    {
        var disabled = settings.DisabledDiskDrives ?? [];
        return GetReadyDrives()
            .Where(d => !IsDriveDisabled(disabled, d.Name))
            .ToList();
    }

    internal static bool IsDriveDisabled(IEnumerable<string> disabledDrives, string driveName) =>
        disabledDrives.Any(d => string.Equals(d, driveName, StringComparison.Ordinal));

    internal static string? SelectMacOsPrimaryDriveName(IEnumerable<string> driveNames)
    {
        var names = driveNames.ToHashSet(StringComparer.Ordinal);

        if (names.Contains(MacOsDataVolumePath))
            return MacOsDataVolumePath;

        if (names.Contains(MacOsRootVolumePath))
            return MacOsRootVolumePath;

        return null;
    }

    internal static DiskVolumeSnapshot CreateAggregatedSnapshot(IReadOnlyList<DriveInfo> drives)
    {
        var totalBytes = drives.Sum(d => d.TotalSize);
        var freeBytes = drives.Sum(d => d.AvailableFreeSpace);

        return new DiskVolumeSnapshot
        {
            Name = AggregatedVolumeName,
            DisplayLabel = AggregatedDisplayLabel,
            PercentUsed = DiskVolumeSnapshot.CalculatePercentUsed(totalBytes, freeBytes),
            DetailLabel = DiskVolumeSnapshot.FormatDetailLabel(totalBytes, freeBytes)
        };
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

    internal static bool DefaultDiskAggregateVolumes() =>
        RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
}

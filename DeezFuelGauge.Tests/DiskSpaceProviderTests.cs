using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class DiskSpaceProviderTests
{
    [Theory]
    [InlineData(1_000_000_000_000, 400_000_000_000, 60)]
    [InlineData(1_000_000_000_000, 0, 100)]
    [InlineData(1_000_000_000_000, 1_000_000_000_000, 0)]
    public void CalculatePercentUsed_returns_expected_values(long totalBytes, long freeBytes, double expected)
    {
        var percent = DiskVolumeSnapshot.CalculatePercentUsed(totalBytes, freeBytes);

        Assert.Equal(expected, percent);
    }

    [Fact]
    public void CalculatePercentUsed_clamps_to_valid_range()
    {
        Assert.Equal(0, DiskVolumeSnapshot.CalculatePercentUsed(0, 0));
        Assert.Equal(0, DiskVolumeSnapshot.CalculatePercentUsed(-1, 0));
        Assert.Equal(100, DiskVolumeSnapshot.CalculatePercentUsed(100, -50));
    }

    [Fact]
    public void FormatGigabytes_uses_one_decimal_place()
    {
        Assert.Equal("1.0 GB", DiskVolumeSnapshot.FormatGigabytes(1_073_741_824));
        Assert.Equal("0.5 GB", DiskVolumeSnapshot.FormatGigabytes(536_870_912));
    }

    [Fact]
    public void FormatDetailLabel_includes_free_and_total()
    {
        var label = DiskVolumeSnapshot.FormatDetailLabel(1_073_741_824, 536_870_912);

        Assert.Equal("0.5 GB free of 1.0 GB", label);
    }

    [Theory]
    [InlineData(@"C:\", "Windows", true, "C: (Windows)")]
    [InlineData(@"C:\", "", true, "C:")]
    [InlineData("/Volumes/Data", "Data", false, "Data")]
    [InlineData("/Volumes/Data", "", false, "/Volumes/Data")]
    public void BuildDisplayLabel_formats_platform_specific_labels(
        string driveName,
        string volumeLabel,
        bool isWindows,
        string expected)
    {
        var label = DiskVolumeSnapshot.BuildDisplayLabel(driveName, volumeLabel, isWindows);

        Assert.Equal(expected, label);
    }

    [Fact]
    public void ToSnapshot_maps_drive_info_fields()
    {
        var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);

        var snapshot = DiskSpaceProvider.ToSnapshot(drive, true);

        Assert.Equal(drive.Name, snapshot.Name);
        Assert.False(string.IsNullOrWhiteSpace(snapshot.DisplayLabel));
        Assert.InRange(snapshot.PercentUsed, 0, 100);
        Assert.Contains("free of", snapshot.DetailLabel);
    }

    [Fact]
    public void GetVolumes_returns_only_ready_drives_with_positive_size()
    {
        var volumes = DiskSpaceProvider.GetVolumes(new WidgetSettings());

        Assert.NotEmpty(volumes);
        Assert.All(volumes, v =>
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Name));
            Assert.False(string.IsNullOrWhiteSpace(v.DisplayLabel));
            Assert.InRange(v.PercentUsed, 0, 100);
            Assert.False(string.IsNullOrWhiteSpace(v.DetailLabel));
        });
    }

    [Fact]
    public void CreateAggregatedSnapshot_sums_bytes_and_percent()
    {
        var drives = DiskSpaceProvider.GetReadyDrives().Take(2).ToList();
        if (drives.Count < 2)
            return;

        var snapshot = DiskSpaceProvider.CreateAggregatedSnapshot(drives);
        var totalBytes = drives.Sum(d => d.TotalSize);
        var freeBytes = drives.Sum(d => d.AvailableFreeSpace);

        Assert.Equal(DiskSpaceProvider.AggregatedVolumeName, snapshot.Name);
        Assert.Equal("All drives", snapshot.DisplayLabel);
        Assert.Equal(DiskVolumeSnapshot.CalculatePercentUsed(totalBytes, freeBytes), snapshot.PercentUsed, precision: 3);
        Assert.Equal(DiskVolumeSnapshot.FormatDetailLabel(totalBytes, freeBytes), snapshot.DetailLabel);
    }

    [Fact]
    public void GetVolumes_filters_disabled_drives()
    {
        var drives = DiskSpaceProvider.GetReadyDrives();
        if (drives.Count < 2)
            return;

        var disabledDrive = drives[1].Name;
        var settings = new WidgetSettings
        {
            DiskAggregateVolumes = false,
            DisabledDiskDrives = [disabledDrive]
        };

        var volumes = DiskSpaceProvider.GetVolumes(settings);

        Assert.DoesNotContain(volumes, v => string.Equals(v.Name, disabledDrive, StringComparison.Ordinal));
    }

    [Fact]
    public void GetVolumes_aggregate_returns_single_snapshot()
    {
        var settings = new WidgetSettings
        {
            DiskAggregateVolumes = true,
            DisabledDiskDrives = []
        };

        var volumes = DiskSpaceProvider.GetVolumes(settings);

        Assert.Single(volumes);
        Assert.Equal(DiskSpaceProvider.AggregatedVolumeName, volumes[0].Name);
    }

    [Fact]
    public void GetVolumes_non_aggregate_returns_individual_snapshots()
    {
        var settings = new WidgetSettings
        {
            DiskAggregateVolumes = false,
            DisabledDiskDrives = []
        };

        var volumes = DiskSpaceProvider.GetVolumes(settings);
        var drives = DiskSpaceProvider.GetReadyDrives();

        Assert.Equal(drives.Count, volumes.Count);
        Assert.All(volumes, v => Assert.NotEqual(DiskSpaceProvider.AggregatedVolumeName, v.Name));
    }

    [Fact]
    public void GetDriveDescriptors_lists_all_ready_drives()
    {
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();
        var drives = DiskSpaceProvider.GetReadyDrives();

        Assert.Equal(drives.Count, descriptors.Count);
        Assert.All(descriptors, d =>
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Name));
            Assert.False(string.IsNullOrWhiteSpace(d.DisplayLabel));
        });
    }

    [Fact]
    public void SelectMacOsPrimaryDriveName_prefers_data_volume()
    {
        var driveNames = new[]
        {
            "/",
            "/System/Volumes/Data",
            "/System/Volumes/VM",
            "/Library/Developer/CoreSimulator/Volumes/iOS_23C54"
        };

        var primary = DiskSpaceProvider.SelectMacOsPrimaryDriveName(driveNames);

        Assert.Equal("/System/Volumes/Data", primary);
    }

    [Fact]
    public void SelectMacOsPrimaryDriveName_falls_back_to_root()
    {
        var driveNames = new[]
        {
            "/",
            "/System/Volumes/VM",
            "/Volumes/External"
        };

        var primary = DiskSpaceProvider.SelectMacOsPrimaryDriveName(driveNames);

        Assert.Equal("/", primary);
    }

    [Fact]
    public void SelectMacOsPrimaryDriveName_returns_null_when_no_primary_volume()
    {
        var driveNames = new[]
        {
            "/System/Volumes/VM",
            "/Volumes/External"
        };

        var primary = DiskSpaceProvider.SelectMacOsPrimaryDriveName(driveNames);

        Assert.Null(primary);
    }

    [Fact]
    public void MigrateDiskSettings_on_macOS_disables_non_primary_drives_when_upgrading()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var settings = new WidgetSettings();
        var json = """{"ShowDiskDrives":true}""";

        SettingsStore.MigrateDiskSettings(settings, json);

        Assert.True(settings.DiskAggregateVolumes);
        Assert.NotEmpty(settings.DisabledDiskDrives);
    }

    [Fact]
    public void MigrateDiskSettings_skips_when_disk_aggregate_already_present()
    {
        var settings = new WidgetSettings { DiskAggregateVolumes = false };
        var json = """{"DiskAggregateVolumes":false}""";

        SettingsStore.MigrateDiskSettings(settings, json);

        Assert.False(settings.DiskAggregateVolumes);
        Assert.Empty(settings.DisabledDiskDrives);
    }
}

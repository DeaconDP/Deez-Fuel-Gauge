using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
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
        var volumes = DiskSpaceProvider.GetVolumes();

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
    public void GetVolumes_excludes_disabled_drives()
    {
        var all = DiskSpaceProvider.GetVolumes();
        if (all.Count == 0)
            return;

        var settings = new WidgetSettings { DisabledDiskDrives = [all[0].Name] };
        var filtered = DiskSpaceProvider.GetVolumes(settings);

        Assert.DoesNotContain(filtered, v =>
            string.Equals(v.Name, all[0].Name, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(all.Count - 1, filtered.Count);
    }

    [Fact]
    public void GetDriveDescriptors_matches_available_volumes()
    {
        var volumes = DiskSpaceProvider.GetVolumes();
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();

        Assert.Equal(volumes.Count, descriptors.Count);
        Assert.Equal(
            volumes.Select(v => v.Name).ToList(),
            descriptors.Select(d => d.Name).ToList());
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
    public void GetMacOsAggregatedVolumes_returns_empty_when_no_primary_volume()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var drives = new[]
        {
            new DriveInfo("/System/Volumes/VM"),
            new DriveInfo("/Volumes/External")
        };

        var volumes = DiskSpaceProvider.GetMacOsAggregatedVolumes(
            drives.Where(d => d.IsReady && d.TotalSize > 0));

        Assert.Empty(volumes);
    }
}

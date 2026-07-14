using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SettingsSectionMapperTests
{
    [Fact]
    public void BuildDiskSection_lists_all_available_drives()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var diskSection = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var driveSources = diskSection.Sources.Where(s => s.Kind == ProviderSourceKind.DiskDrive).ToList();
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();

        Assert.Equal(descriptors.Count, driveSources.Count);
        Assert.Contains(diskSection.Sources, s => s.Kind == ProviderSourceKind.DiskDrives);
        Assert.All(driveSources, s => Assert.False(string.IsNullOrWhiteSpace(s.DrivePath)));
    }

    [Fact]
    public void ApplyDisk_round_trips_disabled_drives()
    {
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();
        if (descriptors.Count == 0)
            return;

        var disabledDrive = descriptors[0].Name;
        var original = new WidgetSettings
        {
            ShowDiskDrives = true,
            ShowDiskDetails = false,
            DisabledDiskDrives = [disabledDrive]
        };

        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        SettingsSectionMapper.PopulateSections(sections, original, host);

        var committed = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, committed, SettingsExpandedProvider.None);

        Assert.True(committed.ShowDiskDrives);
        Assert.False(committed.ShowDiskDetails);
        Assert.Equal([disabledDrive], committed.DisabledDiskDrives);
    }

    [Fact]
    public void OnMasterEnableChanged_does_not_reset_per_drive_toggles()
    {
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();
        if (descriptors.Count < 2)
            return;

        var settings = new WidgetSettings
        {
            ShowDiskDrives = true,
            DisabledDiskDrives = [descriptors[1].Name]
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        var diskSection = viewModel.Sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var driveSources = diskSection.Sources
            .Where(s => s.Kind == ProviderSourceKind.DiskDrive)
            .ToList();
        var firstDrive = driveSources[0];
        var secondDrive = driveSources[1];

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);

        viewModel.OnMasterEnableChanged(diskSection, false);
        viewModel.OnMasterEnableChanged(diskSection, true);

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);
    }

    private static SettingsPanelViewModel CreateViewModel() =>
        new(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            () => new CursorTokens());
}

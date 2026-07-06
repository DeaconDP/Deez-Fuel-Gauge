using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SettingsSectionMapperTests
{
    [Fact]
    public void BuildDiskSection_lists_all_ready_drives()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var diskSection = sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var driveSources = diskSection.Sources.Where(s => s.Kind == ProviderSourceKind.DiskDrive).ToList();
        var descriptors = DiskSpaceProvider.GetDriveDescriptors();

        Assert.Equal(descriptors.Count, driveSources.Count);
        Assert.Contains(diskSection.Sources, s => s.Kind == ProviderSourceKind.DiskDetails);
        Assert.Contains(diskSection.Sources, s => s.Kind == ProviderSourceKind.DiskAggregate);
    }

    [Fact]
    public void ApplyDisk_round_trips_disabled_drives_and_aggregate_flag()
    {
        var drives = DiskSpaceProvider.GetReadyDrives();
        if (drives.Count == 0)
            return;

        var disabledDrive = drives[0].Name;
        var original = new WidgetSettings
        {
            ShowDiskDrives = true,
            ShowDiskDetails = false,
            DiskAggregateVolumes = true,
            DisabledDiskDrives = [disabledDrive]
        };

        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        SettingsSectionMapper.PopulateSections(sections, original, host);

        var committed = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, committed, SettingsExpandedProvider.None);

        Assert.True(committed.ShowDiskDrives);
        Assert.False(committed.ShowDiskDetails);
        Assert.True(committed.DiskAggregateVolumes);
        Assert.Equal([disabledDrive], committed.DisabledDiskDrives);
    }

    [Fact]
    public void OnMasterEnableChanged_does_not_reset_per_drive_toggles()
    {
        var drives = DiskSpaceProvider.GetReadyDrives();
        if (drives.Count < 2)
            return;

        var settings = new WidgetSettings
        {
            ShowDiskDrives = true,
            DiskAggregateVolumes = false,
            DisabledDiskDrives = [drives[1].Name]
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        var diskSection = viewModel.Sections.Single(s => s.ProviderId == SettingsExpandedProvider.Disk);
        var firstDrive = diskSection.Sources.First(s => s.Kind == ProviderSourceKind.DiskDrive);
        var secondDrive = diskSection.Sources.SkipWhile(s => s.Kind != ProviderSourceKind.DiskDrive).Skip(1).First();

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);

        viewModel.OnMasterEnableChanged(diskSection, false);
        viewModel.OnMasterEnableChanged(diskSection, true);

        Assert.True(firstDrive.IsEnabled);
        Assert.False(secondDrive.IsEnabled);
    }

    [Fact]
    public void BuildSystemSection_lists_ram_cpu_and_gpu_sources()
    {
        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();

        SettingsSectionMapper.PopulateSections(sections, new WidgetSettings(), host);

        var systemSection = sections.Single(s => s.ProviderId == SettingsExpandedProvider.System);

        Assert.Contains(systemSection.Sources, s => s.Kind == ProviderSourceKind.SystemDetails);
        Assert.Contains(systemSection.Sources, s => s.Kind == ProviderSourceKind.SystemRam);
        Assert.Contains(systemSection.Sources, s => s.Kind == ProviderSourceKind.SystemCpu);
        Assert.Contains(systemSection.Sources, s => s.Kind == ProviderSourceKind.SystemGpu);
    }

    [Fact]
    public void ApplySystem_round_trips_metric_flags()
    {
        var original = new WidgetSettings
        {
            ShowSystemResources = true,
            ShowSystemDetails = false,
            ShowRam = true,
            ShowCpu = false,
            ShowGpu = true
        };

        var sections = new List<ProviderSettingsSectionViewModel>();
        var host = CreateViewModel();
        SettingsSectionMapper.PopulateSections(sections, original, host);

        var committed = new WidgetSettings();
        SettingsSectionMapper.ApplyToSettings(sections, committed, SettingsExpandedProvider.None);

        Assert.True(committed.ShowSystemResources);
        Assert.False(committed.ShowSystemDetails);
        Assert.True(committed.ShowRam);
        Assert.False(committed.ShowCpu);
        Assert.True(committed.ShowGpu);
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

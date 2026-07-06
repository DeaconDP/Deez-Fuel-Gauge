using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SystemResourceProviderTests
{
    [Theory]
    [InlineData(32_000_000_000, 8_000_000_000, 75)]
    [InlineData(16_000_000_000, 16_000_000_000, 0)]
    [InlineData(16_000_000_000, 0, 100)]
    public void CalculatePercentUsed_returns_expected_values(long totalBytes, long freeBytes, double expected)
    {
        var percent = SystemResourceSnapshot.CalculatePercentUsed(totalBytes, freeBytes);

        Assert.Equal(expected, percent);
    }

    [Fact]
    public void ClampPercent_clamps_to_valid_range()
    {
        Assert.Equal(0, SystemResourceSnapshot.ClampPercent(-5));
        Assert.Equal(100, SystemResourceSnapshot.ClampPercent(150));
        Assert.Equal(42, SystemResourceSnapshot.ClampPercent(42));
    }

    [Fact]
    public void FormatMemoryDetailLabel_includes_free_and_total()
    {
        var label = SystemResourceSnapshot.FormatMemoryDetailLabel(2_147_483_648, 1_073_741_824);

        Assert.Equal("1.0 GB free of 2.0 GB", label);
    }

    [Fact]
    public void FormatCpuDetailLabel_includes_percent_and_core_count()
    {
        var label = SystemResourceSnapshot.FormatCpuDetailLabel(42.4, 8);

        Assert.Equal("42% across 8 cores", label);
    }

    [Fact]
    public void GetMetrics_returns_empty_when_master_disabled()
    {
        var settings = new WidgetSettings
        {
            ShowSystemResources = false,
            ShowRam = true,
            ShowCpu = true,
            ShowGpu = true
        };

        var metrics = SystemResourceProvider.GetMetrics(settings);

        Assert.Empty(metrics);
    }

    [Fact]
    public void GetMetrics_returns_empty_when_all_metrics_disabled()
    {
        var settings = new WidgetSettings
        {
            ShowSystemResources = true,
            ShowRam = false,
            ShowCpu = false,
            ShowGpu = false
        };

        var metrics = SystemResourceProvider.GetMetrics(settings);

        Assert.Empty(metrics);
    }

    [Fact]
    public void GetMetrics_returns_enabled_metrics_on_current_machine()
    {
        var metrics = SystemResourceProvider.GetMetrics(new WidgetSettings());

        Assert.Contains(metrics, m => m.Name == SystemResourceSnapshot.RamName);
        Assert.Contains(metrics, m => m.Name == SystemResourceSnapshot.CpuName);
        Assert.Contains(metrics, m => m.Name == SystemResourceSnapshot.GpuName);

        var ram = metrics.Single(m => m.Name == SystemResourceSnapshot.RamName);
        Assert.True(ram.IsAvailable);
        Assert.InRange(ram.PercentUsed, 0, 100);
        Assert.Contains("free of", ram.DetailLabel);
    }

    [Fact]
    public void ComputeCpuPercent_uses_delta_between_samples()
    {
        SystemResourceProvider.ResetCpuBaselineForTests();

        Assert.Equal(0, SystemResourceProvider.ComputeCpuPercent(100, 1000));
        Assert.Equal(50, SystemResourceProvider.ComputeCpuPercent(150, 1100));
        Assert.Equal(50, SystemResourceProvider.ComputeCpuPercent(200, 1200));
    }

    [Fact]
    public void ComputeCpuPercent_returns_last_value_when_total_delta_is_zero()
    {
        SystemResourceProvider.ResetCpuBaselineForTests();

        _ = SystemResourceProvider.ComputeCpuPercent(100, 1000);
        Assert.Equal(75, SystemResourceProvider.ComputeCpuPercent(125, 1100));
        Assert.Equal(75, SystemResourceProvider.ComputeCpuPercent(125, 1100));
    }
}

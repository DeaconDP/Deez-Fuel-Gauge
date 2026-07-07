using DeezFuelGauge.Models;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class HardwareMetricsSnapshotTests
{
    [Theory]
    [InlineData(0, 100, 0)]
    [InlineData(50, 100, 50)]
    [InlineData(100, 100, 100)]
    [InlineData(150, 100, 100)]
    public void CalculateRamPercent_clamps_to_valid_range(long usedBytes, long totalBytes, double expected)
    {
        var percent = HardwareMetricsSnapshot.CalculateRamPercent(usedBytes, totalBytes);
        Assert.Equal(expected, percent);
    }

    [Fact]
    public void CalculateCpuPercent_uses_idle_delta_over_total()
    {
        var percent = HardwareMetricsSnapshot.CalculateCpuPercent(
            idle1: 100,
            kernel1: 300,
            user1: 200,
            idle2: 150,
            kernel2: 450,
            user2: 350);

        Assert.Equal(83.333333333333329, percent);
    }

    [Fact]
    public void CalculateCpuPercent_returns_null_when_total_is_zero()
    {
        var percent = HardwareMetricsSnapshot.CalculateCpuPercent(1, 1, 1, 1, 1, 1);
        Assert.Null(percent);
    }

    [Fact]
    public void FormatRamDetail_uses_gigabytes()
    {
        var detail = HardwareMetricsSnapshot.FormatRamDetail(10_737_418_240, 34_359_738_368);
        Assert.Equal("10.0 GB / 32.0 GB", detail);
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(72.4, "72°C")]
    public void FormatCpuTemp_formats_or_placeholder(double? celsius, string expected)
    {
        Assert.Equal(expected, HardwareMetricsSnapshot.FormatCpuTemp(celsius));
    }

    [Theory]
    [InlineData(3692, true, 96)]
    [InlineData(369, false, 95.9)]
    [InlineData(2981, true, 25)]
    public void ConvertThermalCounterValue_handles_high_precision_and_kelvin(double raw, bool isHighPrecision, double expected)
    {
        var converted = HardwareMetricsSnapshot.ConvertThermalCounterValue(raw, isHighPrecision);
        Assert.NotNull(converted);
        Assert.Equal(expected, converted!.Value, 0);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(5000)]
    public void ConvertThermalCounterValue_rejects_out_of_range_values(double raw)
    {
        Assert.Null(HardwareMetricsSnapshot.ConvertThermalCounterValue(raw));
    }
}

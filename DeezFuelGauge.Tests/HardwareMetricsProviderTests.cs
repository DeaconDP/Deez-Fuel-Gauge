using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class HardwareMetricsProviderTests
{
    [Fact]
    public void Sample_returns_null_cpu_percent_on_first_reading()
    {
        using var provider = new HardwareMetricsProvider();

        var first = provider.Sample();
        var second = provider.Sample();

        Assert.Null(first.CpuPercent);
        Assert.True(second.RamTotalBytes >= 0);
    }

    [Fact]
    public void ResetCpuBaseline_clears_cpu_warmup_state()
    {
        using var provider = new HardwareMetricsProvider();

        _ = provider.Sample();
        var beforeReset = provider.Sample();
        provider.ResetCpuBaseline();
        var afterReset = provider.Sample();

        Assert.Null(afterReset.CpuPercent);
        Assert.True(afterReset.RamTotalBytes >= 0);
    }
}

using DeezFuelGauge.Services;
using DeezFuelGauge.Services.Hardware;
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

    [Fact]
    public void SelectGpuEngineInstances_keeps_only_3d_engines_up_to_cap()
    {
        var names = new[]
        {
            "pid_1_luid_0x00000000_0x00000000_phys_0_eng_0_engtype_3D",
            "pid_2_luid_0x00000000_0x00000000_phys_0_eng_0_engtype_Copy",
            "pid_3_luid_0x00000000_0x00000000_phys_0_eng_0_engtype_3D",
            "pid_4_luid_0x00000000_0x00000000_phys_0_eng_0_engtype_3D",
        };

        var selected = WindowsHardwareReader.SelectGpuEngineInstances(names, maxCount: 2);

        Assert.Equal(2, selected.Count);
        Assert.All(selected, name => Assert.EndsWith("engtype_3D", name, StringComparison.OrdinalIgnoreCase));
    }
}

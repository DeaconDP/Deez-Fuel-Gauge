using DeezFuelGauge.Models;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderUsageSnapshotTests
{
    [Fact]
    public void FromSpend_computes_percent_of_plan_limit()
    {
        var snapshot = ProviderUsageSnapshot.FromSpend(500, 2000);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(25, snapshot.PercentUsed);
        Assert.Equal("$5.00 of plan", snapshot.DetailLabel);
    }

    [Fact]
    public void Unavailable_returns_unavailable_when_limit_missing()
    {
        var snapshot = ProviderUsageSnapshot.FromSpend(100, 0);

        Assert.False(snapshot.IsAvailable);
    }

    [Fact]
    public void Unavailable_includes_status_message()
    {
        var snapshot = ProviderUsageSnapshot.Unavailable("No plan data");

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("No plan data", snapshot.StatusMessage);
        Assert.Equal("No plan data", snapshot.DetailLabel);
    }
}

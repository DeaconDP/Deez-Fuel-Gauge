using System.Text.Json;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class UsageResponseParserTests
{
    [Fact]
    public void ParseCurrentPeriodUsage_ReturnsBreakdownPercents()
    {
        using var document = JsonDocument.Parse("""
            {
              "planUsage": {
                "limit": 2000,
                "totalPercentUsed": 55,
                "autoPercentUsed": 71,
                "apiPercentUsed": 4,
                "includedSpend": 1100,
                "remaining": 900
              }
            }
            """);

        var snapshot = UsageResponseParser.ParseCurrentPeriodUsage(document.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(55, snapshot.PercentUsed);
        Assert.Equal(71, snapshot.AutoPercentUsed);
        Assert.Equal(4, snapshot.ApiPercentUsed);
        Assert.Equal(2000, snapshot.PlanLimitCents);
        Assert.True(snapshot.HasBreakdown);
        Assert.Equal("$9.00 left", snapshot.RemainingLabel);
    }

    [Fact]
    public void ParseCurrentPeriodUsage_ComputesPercentFromIncludedSpendWhenTotalMissing()
    {
        using var document = JsonDocument.Parse("""
            {
              "planUsage": {
                "limit": 2000,
                "includedSpend": 1000,
                "remaining": 1000
              }
            }
            """);

        var snapshot = UsageResponseParser.ParseCurrentPeriodUsage(document.RootElement);

        Assert.NotNull(snapshot);
        Assert.Equal(50, snapshot.PercentUsed);
        Assert.False(snapshot.HasBreakdown);
    }

    [Fact]
    public void ParseCurrentPeriodUsage_ReturnsNullWhenLimitMissing()
    {
        using var document = JsonDocument.Parse("""{ "planUsage": { "totalPercentUsed": 10 } }""");

        var snapshot = UsageResponseParser.ParseCurrentPeriodUsage(document.RootElement);

        Assert.Null(snapshot);
    }
}

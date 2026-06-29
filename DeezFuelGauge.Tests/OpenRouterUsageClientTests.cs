using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class OpenRouterUsageClientTests
{
    [Fact]
    public void ParseKeyResponse_reads_limit_and_spend()
    {
        const string json = """
            {
              "data": {
                "limit": 100,
                "limit_remaining": 42.5,
                "usage_daily": 1.25,
                "usage_weekly": 4.5,
                "usage_monthly": 12.75
              }
            }
            """;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var parsed = OpenRouterUsageClient.ParseKeyResponse(doc.RootElement);

        Assert.Equal(100, parsed.LimitUsd);
        Assert.Equal(42.5, parsed.LimitRemainingUsd);
        Assert.Equal(1.25, parsed.DailySpendUsd);
        Assert.Equal(4.5, parsed.WeeklySpendUsd);
        Assert.Equal(12.75, parsed.MonthlySpendUsd);
    }

    [Fact]
    public void ParseCreditsResponse_computes_balance()
    {
        const string json = """
            {
              "data": {
                "total_credits": 50,
                "total_usage": 12.34
              }
            }
            """;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var parsed = OpenRouterUsageClient.ParseCreditsResponse(doc.RootElement);

        Assert.NotNull(parsed);
        Assert.Equal(37.66, parsed.Value.BalanceUsd, 2);
    }

    [Fact]
    public void MergeResponses_prefers_key_limit_for_headline()
    {
        var key = new OpenRouterUsageClient.KeyResponseData(100, 25, 0, 0, 0);
        var credits = new OpenRouterUsageClient.CreditsResponseData(80);

        var snapshot = OpenRouterUsageClient.MergeResponses(key, credits);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(75, snapshot.HeadlinePercentUsed);
        Assert.Equal(80, snapshot.BalanceUsd);
    }

    [Fact]
    public void ComputeHeadlinePercent_uses_balance_thresholds_when_no_key_limit()
    {
        Assert.Equal(95, OpenRouterSnapshot.ComputeHeadlinePercent(null, 0.5));
        Assert.Equal(75, OpenRouterSnapshot.ComputeHeadlinePercent(null, 3));
        Assert.Equal(10, OpenRouterSnapshot.ComputeHeadlinePercent(null, 50));
    }
}

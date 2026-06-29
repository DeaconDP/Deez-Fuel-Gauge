using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class OpenCodeUsageClientTests
{
    [Fact]
    public void ParseUsageWindow_reads_percent_and_reset()
    {
        const string html = """
            rollingUsage:$R[12]={usagePercent:42,resetInSec:3600,status:"ok"}
            """;

        var window = OpenCodeUsageClient.ParseUsageWindow(html, "rollingUsage");

        Assert.NotNull(window);
        Assert.True(window.IsAvailable);
        Assert.Equal(42, window.PercentUsed);
        Assert.NotNull(window.ResetsAt);
    }

    [Fact]
    public void ParseGoPage_reads_all_windows()
    {
        const string html = """
            rollingUsage:$R[1]={usagePercent:10,resetInSec:100}
            weeklyUsage:$R[2]={usagePercent:20,resetInSec:200}
            monthlyUsage:$R[3]={usagePercent:30,resetInSec:300}
            """;

        var (hasSubscription, rolling, weekly, monthly) = OpenCodeUsageClient.ParseGoPage(html);

        Assert.True(hasSubscription);
        Assert.NotNull(rolling);
        Assert.NotNull(weekly);
        Assert.NotNull(monthly);
        Assert.Equal(10, rolling!.PercentUsed);
        Assert.Equal(20, weekly!.PercentUsed);
        Assert.Equal(30, monthly!.PercentUsed);
    }

    [Fact]
    public void ParseZenPage_reads_balance_and_monthly_cap()
    {
        const string html = """
            balanceUsd:42.50
            monthlyLimitUsd:100
            monthlyUsage:{usageDollars:25.5}
            """;

        var (balance, monthlyCap, monthlyUsed) = OpenCodeUsageClient.ParseZenPage(html);

        Assert.Equal(42.50m, balance);
        Assert.Equal(100m, monthlyCap);
        Assert.Equal(25.5m, monthlyUsed);
    }

    [Fact]
    public void BuildAuthCookieHeader_prefixes_raw_value()
    {
        Assert.Equal("auth=abc123", OpenCodeUsageClient.BuildAuthCookieHeader("abc123"));
        Assert.Equal("auth=abc123", OpenCodeUsageClient.BuildAuthCookieHeader("auth=abc123"));
    }
}

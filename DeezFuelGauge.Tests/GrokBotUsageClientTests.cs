using System.Net;
using System.Text;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class GrokBotUsageClientTests
{
    [Fact]
    public void ParseSandUsageStatus_reads_weekly_percent_and_reset()
    {
        const string json = """
            {
              "currentPeriodStart": "2026-08-19T10:29:25.725Z",
              "nextResetTimestampUtc": "2026-08-26T10:29:25.725Z",
              "usagePercent": 23.363823,
              "hasAvailableUsage": true,
              "hasNonZeroIncludedLimit": true
            }
            """;

        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var snapshot = GrokBotUsageClient.ParseSandUsageStatus(doc.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(23.363823, snapshot.PercentUsed, 5);
        Assert.Equal(DateTimeOffset.Parse("2026-08-19T10:29:25.725Z"), snapshot.PeriodStart);
        Assert.Equal(DateTimeOffset.Parse("2026-08-26T10:29:25.725Z"), snapshot.ResetsAt);
        Assert.True(snapshot.HasAvailableUsage);
        Assert.True(snapshot.HasNonZeroIncludedLimit);
        Assert.Contains("wk 23%", snapshot.DetailLabel);
    }

    [Fact]
    public void ParseSandUsageStatus_returns_unavailable_when_percent_missing()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""{ "hasAvailableUsage": true }""");
        var snapshot = GrokBotUsageClient.ParseSandUsageStatus(doc.RootElement);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("No Grok Bot usage", snapshot.StatusMessage);
    }

    [Fact]
    public void ParseSandUsageStatus_returns_unavailable_for_pooled_enterprise()
    {
        using var doc = System.Text.Json.JsonDocument.Parse("""
            {
              "usagePercent": 10,
              "usesPooledEnterpriseAllowance": true
            }
            """);
        var snapshot = GrokBotUsageClient.ParseSandUsageStatus(doc.RootElement);

        Assert.False(snapshot.IsAvailable);
        Assert.Contains("enterprise", snapshot.StatusMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromUsage_marks_on_demand_when_included_limit_is_zero()
    {
        var snapshot = GrokBotSnapshot.FromUsage(
            40,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddDays(3),
            hasAvailableUsage: true,
            hasNonZeroIncludedLimit: false);

        Assert.True(snapshot.IsAvailable);
        Assert.Contains("on-demand", snapshot.DetailLabel);
    }

    [Fact]
    public async Task FetchAsync_parses_live_shaped_response()
    {
        // Minimal JWT with exp far in the future so refresh is skipped.
        const string accessToken =
            "eyJhbGciOiJub25lIn0.eyJleHAiOjQ4MDAwMDAwMDB9.";
        var handler = new FixedJsonHandler("""
            {
              "currentPeriodStart": "2026-08-19T10:29:25.725Z",
              "nextResetTimestampUtc": "2026-08-26T10:29:25.725Z",
              "usagePercent": 21.5,
              "hasAvailableUsage": true,
              "hasNonZeroIncludedLimit": true
            }
            """);
        using var client = new GrokBotUsageClient(
            new HttpClient(handler),
            tokenReader: () => new CursorTokens { AccessToken = accessToken, RefreshToken = "refresh-token" });
        client.SetTokens(accessToken, "refresh-token");

        var settings = new ProviderBillingSettings { ShowProLimits = true };
        var snapshot = await client.FetchAsync(settings);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(21.5, snapshot.PercentUsed);
        Assert.Equal("Connected", settings.LastConnectionStatus);
        Assert.Contains("GetSandUsageStatus", handler.LastPath);
    }

    [Fact]
    public async Task FetchAsync_requires_cursor_sign_in()
    {
        using var client = new GrokBotUsageClient(
            new HttpClient(new FixedJsonHandler("{}")),
            tokenReader: () => new CursorTokens());

        var settings = new ProviderBillingSettings();
        var snapshot = await client.FetchAsync(settings);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("Sign in to Cursor", snapshot.StatusMessage);
        Assert.Equal("Sign in to Cursor", settings.LastConnectionStatus);
    }

    private sealed class FixedJsonHandler(string json) : HttpMessageHandler
    {
        public string LastPath { get; private set; } = "";

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastPath = request.RequestUri?.AbsolutePath ?? "";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }
}

using System.Net;
using System.Text;
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
          "limit_reset": "monthly",
          "is_free_tier": false,
          "usage": 57.5,
          "usage_daily": 1.25,
          "usage_weekly": 4.5,
          "usage_monthly": 12.75,
          "include_byok_in_limit": true,
          "byok_usage_daily": 0.5
        }
      }
      """;

    using var doc = System.Text.Json.JsonDocument.Parse(json);
    var parsed = OpenRouterUsageClient.ParseKeyResponse(doc.RootElement);

    Assert.Equal(100, parsed.LimitUsd);
    Assert.Equal(42.5, parsed.LimitRemainingUsd);
    Assert.Equal("monthly", parsed.LimitReset);
    Assert.False(parsed.IsFreeTier);
    Assert.Equal(57.5, parsed.AllTimeUsageUsd);
    Assert.Equal(1.25, parsed.DailySpendUsd);
    Assert.True(parsed.IncludeByokInLimit);
    Assert.Equal(0.5, parsed.ByokDailySpendUsd);
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
    Assert.Equal(50, parsed.Value.TotalCredits);
    Assert.Equal(12.34, parsed.Value.TotalUsage, 2);
  }

  [Fact]
  public void MergeResponses_prefers_key_limit_for_headline()
  {
    var key = new OpenRouterUsageClient.KeyResponseData(
      100, 25, "monthly", false, 75, 0, 0, 0, false, 0);
    var credits = new OpenRouterUsageClient.ClientCreditsResponseData(80, 100, 20);

    var snapshot = OpenRouterUsageClient.MergeResponses(key, credits);

    Assert.True(snapshot.IsAvailable);
    Assert.Equal(75, snapshot.HeadlinePercentUsed);
    Assert.Equal(80, snapshot.BalanceUsd);
    Assert.Contains("monthly", snapshot.DetailLabel);
    Assert.Contains("all-time", snapshot.DetailLabel);
  }

  [Fact]
  public void ComputeHeadlinePercent_uses_key_limit_then_credits_then_zero()
  {
    Assert.Equal(57.5, OpenRouterSnapshot.ComputeHeadlinePercent(57.5, null));

    var credits = new OpenRouterSnapshot.CreditsResponseData(37.66, 50, 12.34);
    Assert.Equal(24.68, OpenRouterSnapshot.ComputeHeadlinePercent(null, credits), 2);

    Assert.Equal(0, OpenRouterSnapshot.ComputeHeadlinePercent(null, null));
  }

  [Fact]
  public void MergeResponses_uses_credits_for_headline_when_no_key_limit()
  {
    var key = new OpenRouterUsageClient.KeyResponseData(
      null, null, null, true, 0, 0, 0, 0, false, 0);
    var credits = new OpenRouterUsageClient.ClientCreditsResponseData(0.5, 1, 0.5);

    var snapshot = OpenRouterUsageClient.MergeResponses(key, credits);

    Assert.True(snapshot.IsAvailable);
    Assert.Equal(50, snapshot.HeadlinePercentUsed);
    Assert.Contains("free tier", snapshot.DetailLabel);
    Assert.Contains("openrouter.ai/activity", snapshot.DetailLabel);
    Assert.Contains("used of", snapshot.DetailLabel);
  }

  [Fact]
  public async Task FetchAsync_prefers_management_key_for_credits()
  {
    var handler = new RecordingHandler(request =>
    {
      if (request.RequestUri!.AbsolutePath == "/api/v1/key")
      {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent("""{"data":{"limit":10,"limit_remaining":5,"usage":5}}""", Encoding.UTF8, "application/json")
        };
      }

      if (request.RequestUri.AbsolutePath == "/api/v1/credits")
      {
        var auth = request.Headers.Authorization?.Parameter;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
          Content = new StringContent(
            auth == "mgmt-key"
              ? """{"data":{"total_credits":100,"total_usage":20}}"""
              : """{"data":{"total_credits":50,"total_usage":10}}""",
            Encoding.UTF8,
            "application/json")
        };
      }

      return new HttpResponseMessage(HttpStatusCode.NotFound);
    });

    using var client = new OpenRouterUsageClient(new HttpClient(handler));
    var apiKeyId = CredentialStore.Store("openrouter-test", "api-key");
    var mgmtKeyId = CredentialStore.Store("openrouter-mgmt-test", "mgmt-key");

    try
    {
      var settings = new ProviderBillingSettings
      {
        CredentialId = apiKeyId,
        ManagementCredentialId = mgmtKeyId
      };

      var snapshot = await client.FetchAsync(settings);

      Assert.True(snapshot.IsAvailable);
      Assert.Equal(80, snapshot.BalanceUsd);
      Assert.Equal(1, handler.CreditsRequestCount);
    }
    finally
    {
      CredentialStore.Delete(apiKeyId);
      CredentialStore.Delete(mgmtKeyId);
    }
  }

  private sealed class RecordingHandler : HttpMessageHandler
  {
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

    public int CreditsRequestCount { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
      if (request.RequestUri?.AbsolutePath == "/api/v1/credits")
        CreditsRequestCount++;

      return Task.FromResult(_handler(request));
    }
  }
}

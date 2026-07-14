using System.Net;
using System.Net.Http;
using System.Text;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class UsageClientTests
{
    private const string ValidJwt =
        "eyJhbGciOiJub25lIn0.eyJleHAiOjk5OTk5OTk5OTksInN1YiI6InVzZXJ8dXNlci0xIn0.";

    [Fact]
    public async Task FetchAsync_without_token_returns_sign_in_error()
    {
        using var client = new UsageClient(new HttpClient(new StubHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))));

        client.SetTokens(null, null);
        var snapshot = await client.FetchAsync();

        Assert.True(snapshot.IsError);
        Assert.Equal("Sign in to Cursor", snapshot.ErrorMessage);
    }

    [Fact]
    public async Task FetchAsync_uses_current_period_usage_when_available()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("GetCurrentPeriodUsage"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "planUsage": {
                            "limit": 2000,
                            "totalPercentUsed": 40,
                            "remaining": 1200
                          }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new UsageClient(new HttpClient(handler));
        client.SetTokens(ValidJwt, "refresh");

        var snapshot = await client.FetchAsync();

        Assert.False(snapshot.IsError);
        Assert.Equal(40, snapshot.PercentUsed);
    }

    [Fact]
    public async Task FetchAsync_falls_back_to_legacy_usage()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("GetCurrentPeriodUsage"))
                return new HttpResponseMessage(HttpStatusCode.NotFound);

            if (request.RequestUri!.AbsolutePath.EndsWith("/auth/usage"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "gpt-4": {
                            "maxRequestUsage": 100,
                            "numRequests": 25
                          }
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new UsageClient(new HttpClient(handler));
        client.SetTokens(ValidJwt, "refresh");

        var snapshot = await client.FetchAsync();

        Assert.False(snapshot.IsError);
        Assert.Equal(25, snapshot.PercentUsed);
        Assert.Equal("75 requests left", snapshot.RemainingLabel);
    }

    [Fact]
    public async Task FetchAsync_enriches_openai_from_aggregations_when_cycle_is_iso()
    {
        var handler = new StubHttpHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("GetCurrentPeriodUsage"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "planUsage": {
                            "limit": 2000,
                            "totalPercentUsed": 40,
                            "remaining": 1200
                          },
                          "billingCycleStart": "2026-04-02T14:11:55.000Z",
                          "billingCycleEnd": "2026-05-02T14:11:55.000Z"
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (path.Contains("GetAggregatedUsageEvents"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """
                        {
                          "aggregations": [
                            { "modelIntent": "gpt-4.1", "totalCents": 400 },
                            { "modelIntent": "gemini-2.5-pro", "totalCents": 100 },
                            { "modelIntent": "claude-4.6-opus-high-thinking", "totalCents": 50 }
                          ]
                        }
                        """,
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var client = new UsageClient(new HttpClient(handler));
        client.SetTokens(ValidJwt, "refresh");

        var snapshot = await client.FetchAsync();

        Assert.False(snapshot.IsError);
        Assert.True(snapshot.OpenAi.IsAvailable);
        Assert.Equal(20, snapshot.OpenAi.PercentUsed);
        Assert.Equal("$4.00 of plan", snapshot.OpenAi.DetailLabel);
        Assert.True(snapshot.Gemini.IsAvailable);
        Assert.Equal(5, snapshot.Gemini.PercentUsed);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}

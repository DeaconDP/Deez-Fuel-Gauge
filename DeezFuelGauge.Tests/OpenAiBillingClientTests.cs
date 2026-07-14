using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class OpenAiBillingClientTests
{
    [Fact]
    public void ParseCostsResponse_sums_amount_values()
    {
        const string json = """
            {
              "data": [
                {
                  "results": [
                    { "amount": { "value": "1.50", "currency": "usd" } },
                    { "amount": { "value": "2.25", "currency": "usd" } }
                  ]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var total = OpenAiBillingClient.ParseCostsResponse(document.RootElement);

        Assert.Equal(3.75, total, 2);
    }

    [Fact]
    public void ParseUsageResponse_sums_token_counts()
    {
        const string json = """
            {
              "data": [
                {
                  "results": [
                    { "input_tokens": 100, "output_tokens": 40 },
                    { "input_tokens": 50, "output_tokens": 10 }
                  ]
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var (input, output) = OpenAiBillingClient.ParseUsageResponse(document.RootElement);

        Assert.Equal(150, input);
        Assert.Equal(50, output);
    }

    [Fact]
    public void ParseCreditGrantsResponse_reads_totals()
    {
        const string json = """
            {
              "object": "credit_summary",
              "total_granted": 18.0,
              "total_used": 9.0,
              "total_available": 9.0
            }
            """;

        using var document = JsonDocument.Parse(json);
        var grants = OpenAiBillingClient.ParseCreditGrantsResponse(document.RootElement);

        Assert.NotNull(grants);
        Assert.Equal(18, grants.Value.GrantedUsd, 2);
        Assert.Equal(9, grants.Value.UsedUsd, 2);
        Assert.Equal(9, grants.Value.RemainingUsd, 2);
    }

    [Fact]
    public void FromCreditGrants_maps_empty_remaining_to_100_percent()
    {
        var snapshot = DirectProviderSnapshot.FromCreditGrants(18, 18, 0);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(100, snapshot.PercentUsed, 2);
        Assert.Contains("$0.00 remaining", snapshot.DetailLabel, StringComparison.Ordinal);
    }

    [Fact]
    public void FromCreditGrants_treats_zero_granted_and_zero_remaining_as_exhausted()
    {
        var snapshot = DirectProviderSnapshot.FromCreditGrants(0, 0, 0);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(100, snapshot.PercentUsed, 2);
    }

    [Fact]
    public async Task FetchAsync_prefers_credit_grants_when_available()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("credit_grants", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"object":"credit_summary","total_granted":20,"total_used":20,"total_available":0}
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"data":[{"results":[{"amount":{"value":"1.00"}}]}]}""", Encoding.UTF8, "application/json")
            };
        });

        var client = new OpenAiBillingClient(new HttpClient(handler));
        var settings = new ProviderBillingSettings
        {
            MonthlyBudgetUsd = 100,
            CredentialId = CredentialStore.Store("openai-grants-test", "sk-test")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings, null, null);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(100, snapshot.PercentUsed, 2);
            Assert.Equal(0, snapshot.RemainingUsd);
            Assert.Contains("remaining", snapshot.DetailLabel, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CredentialStore.Delete(settings.CredentialId);
            client.Dispose();
        }
    }

    [Fact]
    public async Task FetchAsync_falls_back_to_admin_costs_when_credit_grants_rejects_secret_key()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("credit_grants", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden)
                {
                    Content = new StringContent("""
                        {"error":{"message":"Your request to GET /v1/dashboard/billing/credit_grants must be made with a session key (that is, it can only be made from the browser). You made it with the following key type: secret."}}
                        """, Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri!.AbsolutePath.Contains("costs", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"data":[{"results":[{"amount":{"value":"10.00"}}]}]}
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"data":[{"results":[{"input_tokens":1000,"output_tokens":200}]}]}
                    """, Encoding.UTF8, "application/json")
            };
        });

        var client = new OpenAiBillingClient(new HttpClient(handler));
        var settings = new ProviderBillingSettings
        {
            MonthlyBudgetUsd = 100,
            CredentialId = CredentialStore.Store("openai-fallback-test", "sk-test")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings, null, null);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(10, snapshot.SpendUsd, 2);
            Assert.Equal(10, snapshot.PercentUsed, 2);
        }
        finally
        {
            CredentialStore.Delete(settings.CredentialId);
            client.Dispose();
        }
    }

    [Fact]
    public async Task FetchAsync_returns_billing_snapshot_from_mocked_http()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("credit_grants", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"object":"credit_summary"}""", Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri!.AbsolutePath.Contains("costs", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"data":[{"results":[{"amount":{"value":"10.00"}}]}]}
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {"data":[{"results":[{"input_tokens":1000,"output_tokens":200}]}]}
                    """, Encoding.UTF8, "application/json")
            };
        });

        var client = new OpenAiBillingClient(new HttpClient(handler));
        var settings = new ProviderBillingSettings
        {
            MonthlyBudgetUsd = 100,
            CredentialId = CredentialStore.Store("openai-test", "sk-test")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings, null, null);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(10, snapshot.SpendUsd, 2);
            Assert.Equal(10, snapshot.PercentUsed, 2);
            Assert.Equal(1000, snapshot.InputTokens);
            Assert.Equal(200, snapshot.OutputTokens);
        }
        finally
        {
            CredentialStore.Delete(settings.CredentialId);
            client.Dispose();
        }
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

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
    public async Task FetchAsync_returns_billing_snapshot_from_mocked_http()
    {
        var handler = new StubHttpHandler(request =>
        {
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

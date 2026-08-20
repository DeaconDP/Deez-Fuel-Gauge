using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class FalUsageClientTests
{
    [Fact]
    public void ParseBillingResponse_reads_balance_and_currency()
    {
        const string json = """
            {
              "username": "my-team",
              "credits": {
                "current_balance": 24.5,
                "currency": "USD"
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var snapshot = FalUsageClient.ParseBillingResponse(doc.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(24.5, snapshot.BalanceUsd);
        Assert.Equal("USD", snapshot.Currency);
        Assert.Equal(0, snapshot.HeadlinePercentUsed);
        Assert.Equal("$24.50 left", snapshot.DetailLabel);
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(0.5, 95)]
    [InlineData(1, 95)]
    [InlineData(3, 75)]
    [InlineData(5, 75)]
    [InlineData(8, 50)]
    [InlineData(10, 50)]
    [InlineData(25, 0)]
    public void ComputeHeadlinePercent_matches_zen_balance_heuristic(double balance, double expected)
    {
        Assert.Equal(expected, FalSnapshot.ComputeHeadlinePercent(balance));
    }

    [Fact]
    public void FromBalance_empty_tank_when_zero()
    {
        var snapshot = FalSnapshot.FromBalance(0);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(100, snapshot.HeadlinePercentUsed);
        Assert.Equal("$0.00 left", snapshot.DetailLabel);
    }

    [Fact]
    public async Task FetchAsync_returns_unavailable_when_key_missing()
    {
        using var client = new FalUsageClient(new HttpClient(new AlwaysOkHandler()));
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        var snapshot = await client.FetchAsync(settings);

        Assert.False(snapshot.IsAvailable);
        Assert.Equal("Admin API key not set", snapshot.StatusMessage);
    }

    [Fact]
    public async Task FetchAsync_reads_billing_with_key_auth()
    {
        var handler = new RecordingHandler(request =>
        {
            Assert.Equal("Key", request.Headers.Authorization?.Scheme);
            Assert.Equal("fal-admin-test", request.Headers.Authorization?.Parameter);
            Assert.Contains("expand=credits", request.RequestUri!.Query);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"username":"u","credits":{"current_balance":4.2,"currency":"USD"}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var credentialId = CredentialStore.Store("fal", "fal-admin-test");
        try
        {
            using var client = new FalUsageClient(new HttpClient(handler));
            var settings = new ProviderBillingSettings
            {
                ShowProLimits = true,
                CredentialId = credentialId
            };

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(4.2, snapshot.BalanceUsd);
            Assert.Equal(75, snapshot.HeadlinePercentUsed);
            Assert.Equal("Connected", settings.LastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(credentialId);
        }
    }

    [Fact]
    public async Task TestConnectionAsync_reports_forbidden_as_non_admin()
    {
        var handler = new RecordingHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Forbidden));

        using var client = new FalUsageClient(new HttpClient(handler));
        var status = await client.TestConnectionAsync("bad-key");

        Assert.Equal("Invalid or non-Admin API key", status);
    }

    private sealed class AlwaysOkHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"username":"u"}""", Encoding.UTF8, "application/json")
            });
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) =>
            _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responder(request));
    }
}

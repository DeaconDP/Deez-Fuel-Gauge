using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeProUsageClientTests
{
    [Theory]
    [InlineData(0.42, 42)]
    [InlineData(1, 100)]
    [InlineData(42, 42)]
    [InlineData(0, 0)]
    public void NormalizeUtilization_handles_fraction_and_percent(double input, double expected)
    {
        var result = ClaudeProUsageClient.NormalizeUtilization(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseOrgUuid_reads_organization_uuid_from_memberships()
    {
        const string json = """
            {
              "memberships": [
                {
                  "organization": { "uuid": "org-abc-123" }
                }
              ]
            }
            """;

        using var document = JsonDocument.Parse(json);
        var uuid = ClaudeProUsageClient.ParseOrgUuid(document.RootElement);

        Assert.Equal("org-abc-123", uuid);
    }

    [Fact]
    public void ParseUsageResponse_reads_session_and_weekly_windows()
    {
        const string json = """
            {
              "five_hour": { "utilization": 0.12, "resets_at": "2026-06-24T18:00:00Z" },
              "seven_day": { "utilization": 34, "resets_at": "2026-07-01T00:00:00Z" }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = ClaudeProUsageClient.ParseUsageResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(12, snapshot.SessionPercentUsed, 1);
        Assert.Equal(34, snapshot.WeeklyPercentUsed, 1);
        Assert.Equal("2026-06-24T18:00:00Z", snapshot.SessionResetsAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        Assert.Equal("2026-07-01T00:00:00Z", snapshot.WeeklyResetsAt?.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        Assert.Contains("5h 12%", snapshot.DetailLabel);
        Assert.Contains("wk 34%", snapshot.DetailLabel);
    }

    [Fact]
    public void BuildCookieHeader_wraps_raw_session_value()
    {
        Assert.Equal("sessionKey=abc123", ClaudeProUsageClient.BuildCookieHeader("abc123"));
        Assert.Equal("sessionKey=abc123", ClaudeProUsageClient.BuildCookieHeader("sessionKey=abc123"));
    }

    [Fact]
    public async Task FetchAsync_returns_usage_from_mocked_http()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {"memberships":[{"organization":{"uuid":"org-1"}}]}
                        """, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                    {
                      "five_hour": { "utilization": 0.25 },
                      "seven_day": { "utilization": 0.5 }
                    }
                    """, Encoding.UTF8, "application/json")
            };
        });

        var client = new ClaudeProUsageClient(new HttpClient(handler));
        var settings = new ProviderBillingSettings
        {
            ProSessionCredentialId = CredentialStore.Store("claude-pro-test", "test-session")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(25, snapshot.SessionPercentUsed, 1);
            Assert.Equal(50, snapshot.WeeklyPercentUsed, 1);
            Assert.Equal("Connected", settings.ProLastConnectionStatus);
        }
        finally
        {
            CredentialStore.Delete(settings.ProSessionCredentialId);
        }
    }

    [Fact]
    public async Task FetchAsync_uses_oauth_usage_endpoint_for_claude_code_auth()
    {
        var handler = new StubHttpHandler(request =>
        {
            Assert.Equal("/api/oauth/usage", request.RequestUri!.AbsolutePath);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("oauth-token", request.Headers.Authorization?.Parameter);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.1},"seven_day":{"utilization":0.2}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token" },
                browserCookieReader: () => null,
                savedSessionReader: _ => null));

        var settings = new ProviderBillingSettings();
        var snapshot = await client.FetchAsync(settings);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(10, snapshot.SessionPercentUsed, 1);
        Assert.Equal(20, snapshot.WeeklyPercentUsed, 1);
    }

    [Fact]
    public async Task RefreshAndConnectAsync_persists_browser_cookie()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"memberships":[{"organization":{"uuid":"org-1"}}]}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.3},"seven_day":{"utilization":0.4}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var client = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                browserCookieReader: () => "browser-session",
                savedSessionReader: _ => null));

        var settings = new ProviderBillingSettings();

        try
        {
            var status = await client.RefreshAndConnectAsync(settings);

            Assert.Equal("Connected", status);
            Assert.False(string.IsNullOrWhiteSpace(settings.ProSessionCredentialId));
            Assert.Equal("browser-session", CredentialStore.Retrieve(settings.ProSessionCredentialId));
        }
        finally
        {
            CredentialStore.Delete(settings.ProSessionCredentialId);
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

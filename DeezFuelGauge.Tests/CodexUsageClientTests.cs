using System.Net;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CodexUsageClientTests
{
    private const string SampleAuthJson = """
        {
          "auth_mode": "chatgpt",
          "tokens": {
            "access_token": "eyJhbGciOiJub25lIn0.eyJjaGF0Z3B0X2FjY291bnRfaWQiOiJhY2MtMTIzIn0.",
            "account_id": "acc-from-file"
          }
        }
        """;

    private const string SampleUsageJson = """
        {
          "plan_type": "plus",
          "rate_limit": {
            "limit_reached": false,
            "primary_window": {
              "used_percent": 2.0,
              "limit_window_seconds": 18000,
              "reset_after_seconds": 7200
            },
            "secondary_window": {
              "used_percent": 1.0,
              "limit_window_seconds": 604800,
              "reset_after_seconds": 432000
            }
          },
          "credits": {
            "balance": "0"
          }
        }
        """;

    [Fact]
    public void ParseAuthJson_extracts_token_and_account_id()
    {
        var auth = CodexUsageClient.ParseAuthJson(SampleAuthJson);

        Assert.NotNull(auth);
        Assert.StartsWith("eyJ", auth.Value.AccessToken);
        Assert.Equal("acc-from-file", auth.Value.AccountId);
    }

    [Fact]
    public void ParseAuthJson_returns_null_for_api_key_mode()
    {
        const string json = """{"auth_mode":"api_key","tokens":{"access_token":"sk-test"}}""";

        Assert.Null(CodexUsageClient.ParseAuthJson(json));
    }

    [Fact]
    public void ParseUsageResponse_maps_primary_and_secondary_windows()
    {
        var observedAt = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        using var document = JsonDocument.Parse(SampleUsageJson);
        var snapshot = CodexUsageClient.ParseUsageResponse(document.RootElement, observedAt);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal("Plus", snapshot.PlanLabel);
        Assert.Equal(98, snapshot.SessionPercentRemaining, 1);
        Assert.Equal(99, snapshot.WeeklyPercentRemaining, 1);
        Assert.Equal(0m, snapshot.CreditsBalanceUsd);
        Assert.False(snapshot.LimitReached);
        Assert.Equal(observedAt.AddHours(2), snapshot.SessionResetsAt);
        Assert.Equal(observedAt.AddDays(5), snapshot.WeeklyResetsAt);
        Assert.Contains("Plus", snapshot.DetailLabel);
        Assert.Contains("5h 2%", snapshot.DetailLabel);
        Assert.Contains("wk 1%", snapshot.DetailLabel);
        Assert.Equal(2, snapshot.SessionPercentUsed, 1);
        Assert.Equal(1, snapshot.WeeklyPercentUsed, 1);
    }

    [Fact]
    public void ParseUsageResponse_reads_reset_at_epoch()
    {
        const string json = """
            {
              "plan_type": "pro",
              "rate_limit": {
                "primary_window": { "used_percent": 50, "reset_at": 1780000000 }
              }
            }
            """;

        using var document = JsonDocument.Parse(json);
        var snapshot = CodexUsageClient.ParseUsageResponse(document.RootElement);

        Assert.True(snapshot.IsAvailable);
        Assert.Equal(50, snapshot.SessionPercentRemaining, 1);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1780000000), snapshot.SessionResetsAt);
    }

    [Fact]
    public void BuildCookieHeader_wraps_raw_session_value()
    {
        Assert.Equal("__Secure-next-auth.session-token=abc123", CodexUsageClient.BuildCookieHeader("abc123"));
        Assert.Equal("token=abc", CodexUsageClient.BuildCookieHeader("token=abc"));
        Assert.Equal("token=abc", CodexUsageClient.BuildCookieHeader("Cookie: token=abc"));
    }

    [Fact]
    public void ParseSessionResponse_reads_access_token_and_account_id_from_jwt()
    {
        const string json = """
            {
              "accessToken": "eyJhbGciOiJub25lIn0.eyJjaGF0Z3B0X2FjY291bnRfaWQiOiJhY2MtZnJvbS1qd3QifQ."
            }
            """;

        using var document = JsonDocument.Parse(json);
        var auth = CodexUsageClient.ParseSessionResponse(document.RootElement);

        Assert.NotNull(auth);
        Assert.Equal("acc-from-jwt", auth.Value.AccountId);
    }

    [Fact]
    public async Task FetchAsync_returns_snapshot_from_auth_file_and_mocked_http()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-auth-test-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(authPath, SampleAuthJson);

        try
        {
            var handler = new StubHttpHandler(request =>
            {
                Assert.Equal("/backend-api/wham/usage", request.RequestUri!.AbsolutePath);
                Assert.Equal("acc-from-file", request.Headers.GetValues("chatgpt-account-id").Single());

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(SampleUsageJson, Encoding.UTF8, "application/json")
                };
            });

            var client = new CodexUsageClient(new HttpClient(handler), () => authPath);
            var settings = new DeezFuelGauge.Models.ProviderBillingSettings();

            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal(98, snapshot.SessionPercentRemaining, 1);
            Assert.Equal(99, snapshot.WeeklyPercentRemaining, 1);
            Assert.Equal("Connected (Plus)", settings.ProLastConnectionStatus);
        }
        finally
        {
            File.Delete(authPath);
        }
    }

    [Fact]
    public async Task FetchAsync_uses_session_cookie_when_auth_file_missing()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/auth/session")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        {
                          "accessToken": "eyJhbGciOiJub25lIn0.eyJjaGF0Z3B0X2FjY291bnRfaWQiOiJzZXNzLWFjYyJ9.",
                          "account": { "id": "sess-acc" }
                        }
                        """, Encoding.UTF8, "application/json")
                };
            }

            Assert.Equal("sess-acc", request.Headers.GetValues("chatgpt-account-id").Single());
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleUsageJson, Encoding.UTF8, "application/json")
            };
        });

        var client = new CodexUsageClient(new HttpClient(handler), () => null);
        var settings = new DeezFuelGauge.Models.ProviderBillingSettings
        {
            ProSessionCredentialId = CredentialStore.Store("openai-codex-test", "session-token")
        };

        try
        {
            var snapshot = await client.FetchAsync(settings);

            Assert.True(snapshot.IsAvailable);
            Assert.Equal("Connected (Plus)", settings.ProLastConnectionStatus);
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

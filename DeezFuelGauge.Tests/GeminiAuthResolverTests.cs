using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class GeminiCliTokenReaderTests
{
    [Fact]
    public void ParseOAuthCredentials_reads_access_refresh_and_expiry()
    {
        const string json = """
            {
              "access_token": "access-1",
              "refresh_token": "refresh-1",
              "expiry_date": 4102444800000
            }
            """;

        var tokens = GeminiCliTokenReader.ParseOAuthCredentials(json);

        Assert.Equal("access-1", tokens.AccessToken);
        Assert.Equal("refresh-1", tokens.RefreshToken);
        Assert.NotNull(tokens.ExpiresAt);
    }

    [Fact]
    public void ParseOAuthCredentials_returns_empty_for_invalid_json()
    {
        var tokens = GeminiCliTokenReader.ParseOAuthCredentials("not-json");
        Assert.Null(tokens.AccessToken);
        Assert.Null(tokens.RefreshToken);
    }
}

public sealed class GeminiAuthResolverTests
{
    [Fact]
    public void Resolve_prefers_antigravity_over_gemini_cli()
    {
        var resolver = new GeminiAuthResolver(
            antigravityReader: () => new AntigravityOAuthTokens { AccessToken = "ag-token" },
            geminiCliReader: () => new AntigravityOAuthTokens { AccessToken = "cli-token" });

        var result = resolver.Resolve();

        Assert.Equal(GeminiAuthSource.Antigravity, result.Source);
        Assert.Equal("ag-token", result.Tokens.AccessToken);
    }

    [Fact]
    public void Resolve_uses_gemini_cli_when_antigravity_missing()
    {
        var resolver = new GeminiAuthResolver(
            antigravityReader: () => new AntigravityOAuthTokens(),
            geminiCliReader: () => new AntigravityOAuthTokens { RefreshToken = "cli-refresh" });

        var result = resolver.Resolve();

        Assert.Equal(GeminiAuthSource.GeminiCli, result.Source);
        Assert.Equal("cli-refresh", result.Tokens.RefreshToken);
    }

    [Fact]
    public void HasDetectableAuth_returns_false_when_both_missing()
    {
        var resolver = new GeminiAuthResolver(
            antigravityReader: () => new AntigravityOAuthTokens(),
            geminiCliReader: () => new AntigravityOAuthTokens());

        Assert.False(resolver.HasDetectableAuth());
    }
}

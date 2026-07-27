using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeCodeTokenReaderTests
{
    [Fact]
    public void EnumerateKeychainServiceNames_yields_hashed_then_bare()
    {
        var names = ClaudeCodeTokenReader.EnumerateKeychainServiceNames("/Users/example/.claude").ToArray();

        Assert.Equal(2, names.Length);
        Assert.Equal(ClaudeCodeTokenReader.BuildKeychainServiceName("/Users/example/.claude"), names[0]);
        Assert.Equal("Claude Code-credentials", names[1]);
        Assert.StartsWith("Claude Code-credentials-", names[0], StringComparison.Ordinal);
        Assert.NotEqual(names[0], names[1]);
    }

    [Fact]
    public void BuildKeychainServiceName_is_stable_for_same_config_dir()
    {
        var a = ClaudeCodeTokenReader.BuildKeychainServiceName("/Users/example/.claude");
        var b = ClaudeCodeTokenReader.BuildKeychainServiceName("/Users/example/.claude");

        Assert.Equal(a, b);
        Assert.Matches("^Claude Code-credentials-[0-9a-f]{8}$", a);
    }

    [Fact]
    public void ParseCredentialsJson_reads_access_token_and_expiry()
    {
        var json = """
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat01-test",
                "expiresAt": 9999999999999
              }
            }
            """;

        var credential = ClaudeCodeTokenReader.ParseCredentialsJson(json);

        Assert.NotNull(credential);
        Assert.Equal("sk-ant-oat01-test", credential!.AccessToken);
        Assert.Equal(9999999999999, credential.ExpiresAt);
        Assert.False(credential.IsExpired);
    }

    [Fact]
    public void ParseCredentialsJson_returns_null_for_invalid_payload()
    {
        Assert.Null(ClaudeCodeTokenReader.ParseCredentialsJson("{}"));
        Assert.Null(ClaudeCodeTokenReader.ParseCredentialsJson("not-json"));
    }
}

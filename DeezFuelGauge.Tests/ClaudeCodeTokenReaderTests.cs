using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeCodeTokenReaderTests
{
    [Fact]
    public void ParseCredentialsJson_reads_access_token_and_expiry()
    {
        const string json = """
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat01-test",
                "expiresAt": 4102444800000
              }
            }
            """;

        var credential = ClaudeCodeTokenReader.ParseCredentialsJson(json);

        Assert.NotNull(credential);
        Assert.Equal("sk-ant-oat01-test", credential.AccessToken);
        Assert.False(credential.IsExpired);
    }

    [Fact]
    public void ParseCredentialsJson_returns_null_when_access_token_missing()
    {
        const string json = """{ "claudeAiOauth": { "refreshToken": "x" } }""";

        Assert.Null(ClaudeCodeTokenReader.ParseCredentialsJson(json));
    }

    [Fact]
    public void ParseCredentialsJson_marks_expired_tokens()
    {
        const string json = """
            {
              "claudeAiOauth": {
                "accessToken": "sk-ant-oat01-test",
                "expiresAt": 1
              }
            }
            """;

        var credential = ClaudeCodeTokenReader.ParseCredentialsJson(json);

        Assert.NotNull(credential);
        Assert.True(credential.IsExpired);
    }

    [Fact]
    public void BuildKeychainServiceName_uses_config_dir_hash()
    {
        var service = ClaudeCodeTokenReader.BuildKeychainServiceName("/tmp/.claude");
        Assert.StartsWith("Claude Code-credentials-", service, StringComparison.Ordinal);
        Assert.Equal(32, service.Length);
    }
}

using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeProAuthResolverTests
{
    [Fact]
    public void Resolve_prefers_claude_code_oauth_over_saved_session()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token" },
            browserCookieReader: () => "browser-cookie",
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.ClaudeCodeOAuth, result.Source);
        Assert.Equal("oauth-token", result.OAuthAccessToken);
    }

    [Fact]
    public void Resolve_uses_browser_cookie_when_oauth_missing()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            browserCookieReader: () => "browser-cookie",
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.BrowserCookie, result.Source);
        Assert.Equal("browser-cookie", result.SessionCookie);
    }

    [Fact]
    public void Resolve_uses_saved_session_when_other_sources_missing()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            browserCookieReader: () => null,
            savedSessionReader: _ => "saved-session");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(ClaudeProAuthSource.SavedSession, result.Source);
        Assert.Equal("saved-session", result.SessionCookie);
    }

    [Fact]
    public void Resolve_returns_failure_when_no_auth_found()
    {
        var resolver = new ClaudeProAuthResolver(
            claudeCodeReader: () => null,
            browserCookieReader: () => null,
            savedSessionReader: _ => null);

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.False(result.HasAuth);
        Assert.Contains("Refresh", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PersistBrowserSession_stores_encrypted_credential_id()
    {
        var settings = new ProviderBillingSettings();

        try
        {
            ClaudeProAuthResolver.PersistBrowserSession(settings, "session-key");

            Assert.False(string.IsNullOrWhiteSpace(settings.ProSessionCredentialId));
            Assert.Equal("session-key", CredentialStore.Retrieve(settings.ProSessionCredentialId));
        }
        finally
        {
            CredentialStore.Delete(settings.ProSessionCredentialId);
        }
    }
}

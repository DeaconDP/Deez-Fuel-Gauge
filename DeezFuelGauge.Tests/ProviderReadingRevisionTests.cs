using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CodexAuthResolverTests
{
    [Fact]
    public void Resolve_prefers_auth_file_over_browser_cookie()
    {
        var resolver = new CodexAuthResolver(
            authFileReader: () => new CodexAuth("token", "acc"),
            browserCookieReader: () => "browser",
            savedSessionReader: _ => "saved");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(CodexAuthSource.AuthFile, result.Source);
        Assert.Equal("token", result.Auth?.AccessToken);
    }

    [Fact]
    public void Resolve_uses_browser_cookie_when_auth_file_missing()
    {
        var resolver = new CodexAuthResolver(
            authFileReader: () => null,
            browserCookieReader: () => "browser-cookie",
            savedSessionReader: _ => null);

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(CodexAuthSource.BrowserCookie, result.Source);
        Assert.Equal("browser-cookie", result.SessionCookie);
    }
}

public sealed class OpenCodeAuthResolverTests
{
    [Fact]
    public void Resolve_prefers_api_key_over_saved_session()
    {
        var resolver = new OpenCodeAuthResolver(
            apiKeyReader: () => "sk-cli",
            browserCookieReader: () => "browser",
            savedSessionReader: _ => "saved");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(OpenCodeAuthSource.ApiKey, result.Source);
        Assert.Equal("sk-cli", result.ApiKey);
    }

    [Fact]
    public void Resolve_uses_saved_session_when_api_key_missing()
    {
        var resolver = new OpenCodeAuthResolver(
            apiKeyReader: () => null,
            browserCookieReader: () => null,
            savedSessionReader: _ => "saved-cookie");

        var result = resolver.Resolve(new ProviderBillingSettings());

        Assert.Equal(OpenCodeAuthSource.SavedSession, result.Source);
        Assert.Equal("saved-cookie", result.SessionCookie);
    }
}

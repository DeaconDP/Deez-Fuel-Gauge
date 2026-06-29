using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public enum ClaudeProAuthSource
{
    None,
    ClaudeCodeOAuth,
    BrowserCookie,
    SavedSession
}

public sealed class ClaudeProAuthResult
{
    public ClaudeProAuthSource Source { get; init; }
    public string? OAuthAccessToken { get; init; }
    public string? SessionCookie { get; init; }
    public string? FailureMessage { get; init; }

    public bool HasAuth =>
        !string.IsNullOrWhiteSpace(OAuthAccessToken) || !string.IsNullOrWhiteSpace(SessionCookie);

    public static ClaudeProAuthResult FromOAuth(string accessToken) => new()
    {
        Source = ClaudeProAuthSource.ClaudeCodeOAuth,
        OAuthAccessToken = accessToken
    };

    public static ClaudeProAuthResult FromBrowserCookie(string sessionCookie) => new()
    {
        Source = ClaudeProAuthSource.BrowserCookie,
        SessionCookie = sessionCookie
    };

    public static ClaudeProAuthResult FromSavedSession(string sessionCookie) => new()
    {
        Source = ClaudeProAuthSource.SavedSession,
        SessionCookie = sessionCookie
    };

    public static ClaudeProAuthResult Failed(string message) => new()
    {
        Source = ClaudeProAuthSource.None,
        FailureMessage = message
    };
}

public sealed class ClaudeProAuthResolver
{
    private readonly Func<ClaudeCodeOAuthCredential?> _claudeCodeReader;
    private readonly Func<string?> _browserCookieReader;
    private readonly Func<string?, string?> _savedSessionReader;

    public ClaudeProAuthResolver(
        Func<ClaudeCodeOAuthCredential?>? claudeCodeReader = null,
        Func<string?>? browserCookieReader = null,
        Func<string?, string?>? savedSessionReader = null)
    {
        _claudeCodeReader = claudeCodeReader ?? ClaudeCodeTokenReader.Read;
        _browserCookieReader = browserCookieReader ?? (() => new ClaudeBrowserCookieReader().ReadSessionKey());
        _savedSessionReader = savedSessionReader ?? (id => CredentialStore.Retrieve(id));
    }

    public ClaudeProAuthResult Resolve(ProviderBillingSettings settings, bool tryBrowserCookies = true)
    {
        var oauth = _claudeCodeReader();
        if (oauth is not null)
        {
            if (oauth.IsExpired)
                return ClaudeProAuthResult.Failed("Claude Code token expired — run claude login again");

            return ClaudeProAuthResult.FromOAuth(oauth.AccessToken);
        }

        if (tryBrowserCookies)
        {
            try
            {
                var browserCookie = _browserCookieReader();
                if (!string.IsNullOrWhiteSpace(browserCookie))
                    return ClaudeProAuthResult.FromBrowserCookie(browserCookie);
            }
            catch (IOException)
            {
                return ClaudeProAuthResult.Failed("Close Chrome or Edge, then click Refresh again");
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return ClaudeProAuthResult.Failed("Close Chrome or Edge, then click Refresh again");
            }
        }

        var savedSession = _savedSessionReader(settings.ProSessionCredentialId);
        if (!string.IsNullOrWhiteSpace(savedSession))
            return ClaudeProAuthResult.FromSavedSession(savedSession);

        return ClaudeProAuthResult.Failed("Sign in at claude.ai, then click Refresh");
    }

    public static void PersistBrowserSession(ProviderBillingSettings settings, string sessionCookie)
    {
        CredentialStore.Replace(
            "claude-pro",
            settings.ProSessionCredentialId,
            sessionCookie,
            id => settings.ProSessionCredentialId = id);
    }
}

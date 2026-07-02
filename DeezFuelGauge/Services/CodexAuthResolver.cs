using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public enum CodexAuthSource
{
    None,
    AuthFile,
    BrowserCookie,
    SavedSession
}

public sealed class CodexAuthResult
{
    public CodexAuthSource Source { get; init; }
    public CodexAuth? Auth { get; init; }
    public string? SessionCookie { get; init; }
    public string? FailureMessage { get; init; }

    public bool HasAuthFile => Auth is not null;
    public bool HasSessionCookie => !string.IsNullOrWhiteSpace(SessionCookie);

    public static CodexAuthResult FromAuthFile(CodexAuth auth) => new()
    {
        Source = CodexAuthSource.AuthFile,
        Auth = auth
    };

    public static CodexAuthResult FromBrowserCookie(string sessionCookie) => new()
    {
        Source = CodexAuthSource.BrowserCookie,
        SessionCookie = sessionCookie
    };

    public static CodexAuthResult FromSavedSession(string sessionCookie) => new()
    {
        Source = CodexAuthSource.SavedSession,
        SessionCookie = sessionCookie
    };

    public static CodexAuthResult Failed(string message) => new()
    {
        Source = CodexAuthSource.None,
        FailureMessage = message
    };
}

public sealed class CodexAuthResolver
{
    private readonly Func<CodexAuth?> _authFileReader;
    private readonly Func<string?> _browserCookieReader;
    private readonly Func<string?, string?> _savedSessionReader;

    public CodexAuthResolver(
        Func<CodexAuth?>? authFileReader = null,
        Func<string?>? browserCookieReader = null,
        Func<string?, string?>? savedSessionReader = null)
    {
        _authFileReader = authFileReader ?? (() => CodexUsageClient.TryReadLocalAuthFile(out var auth, null) ? auth : null);
        _browserCookieReader = browserCookieReader ?? (() => new ChatGptBrowserCookieReader().ReadSessionToken());
        _savedSessionReader = savedSessionReader ?? (id => CredentialStore.Retrieve(id));
    }

    public CodexAuthResult Resolve(ProviderBillingSettings settings, bool tryBrowserCookies = true)
    {
        var fromFile = _authFileReader();
        if (fromFile is not null)
            return CodexAuthResult.FromAuthFile(fromFile.Value);

        if (tryBrowserCookies)
        {
            try
            {
                var browserCookie = _browserCookieReader();
                if (!string.IsNullOrWhiteSpace(browserCookie))
                    return CodexAuthResult.FromBrowserCookie(browserCookie);
            }
            catch (IOException)
            {
                return CodexAuthResult.Failed("Close Chrome or Edge, then click Refresh again");
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return CodexAuthResult.Failed("Close Chrome or Edge, then click Refresh again");
            }
        }

        var savedSession = _savedSessionReader(settings.ProSessionCredentialId);
        if (!string.IsNullOrWhiteSpace(savedSession))
            return CodexAuthResult.FromSavedSession(savedSession);

        return CodexAuthResult.Failed("Codex auth not found — run codex login or paste session cookie");
    }

    public static void PersistBrowserSession(ProviderBillingSettings settings, string sessionCookie)
    {
        CredentialStore.Replace(
            "openai-codex",
            settings.ProSessionCredentialId,
            sessionCookie,
            id => settings.ProSessionCredentialId = id);
    }

    public bool HasDetectableAuth(ProviderBillingSettings settings)
    {
        if (_authFileReader() is not null)
            return true;

        try
        {
            if (!string.IsNullOrWhiteSpace(_browserCookieReader()))
                return true;
        }
        catch (IOException)
        {
            // browser locked
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
            // browser locked
        }

        return !string.IsNullOrWhiteSpace(_savedSessionReader(settings.ProSessionCredentialId));
    }
}

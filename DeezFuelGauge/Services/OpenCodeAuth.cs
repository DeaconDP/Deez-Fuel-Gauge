namespace DeezFuelGauge.Services;

public enum OpenCodeAuthSource
{
    None,
    ApiKey,
    BrowserCookie,
    SavedSession
}

public sealed class OpenCodeAuthResult
{
    public OpenCodeAuthSource Source { get; init; }
    public string? ApiKey { get; init; }
    public string? SessionCookie { get; init; }
    public string? FailureMessage { get; init; }

    public static OpenCodeAuthResult FromApiKey(string apiKey) => new()
    {
        Source = OpenCodeAuthSource.ApiKey,
        ApiKey = apiKey
    };

    public static OpenCodeAuthResult FromBrowserCookie(string sessionCookie) => new()
    {
        Source = OpenCodeAuthSource.BrowserCookie,
        SessionCookie = sessionCookie
    };

    public static OpenCodeAuthResult FromSavedSession(string sessionCookie) => new()
    {
        Source = OpenCodeAuthSource.SavedSession,
        SessionCookie = sessionCookie
    };

    public static OpenCodeAuthResult Failed(string message) => new()
    {
        Source = OpenCodeAuthSource.None,
        FailureMessage = message
    };
}

namespace CursorUsageWidget.Services;

public enum GeminiAuthSource
{
    None,
    Antigravity,
    GeminiCli
}

public sealed class GeminiAuthResult
{
    public GeminiAuthSource Source { get; init; }
    public AntigravityOAuthTokens Tokens { get; init; } = new();
    public string? OAuthClientId { get; init; }
    public string? OAuthClientSecret { get; init; }
    public string? FailureMessage { get; init; }

    public bool HasAuth =>
        !string.IsNullOrWhiteSpace(Tokens.AccessToken) || !string.IsNullOrWhiteSpace(Tokens.RefreshToken);

    public static GeminiAuthResult FromAntigravity(AntigravityOAuthTokens tokens) => new()
    {
        Source = GeminiAuthSource.Antigravity,
        Tokens = tokens,
        OAuthClientId = AntigravityOAuthAppCredentials.ClientId,
        OAuthClientSecret = AntigravityOAuthAppCredentials.ClientSecret
    };

    public static GeminiAuthResult FromGeminiCli(AntigravityOAuthTokens tokens) => new()
    {
        Source = GeminiAuthSource.GeminiCli,
        Tokens = tokens,
        OAuthClientId = GeminiCliOAuthAppCredentials.ClientId,
        OAuthClientSecret = GeminiCliOAuthAppCredentials.ClientSecret
    };

    public static GeminiAuthResult Failed(string message) => new()
    {
        Source = GeminiAuthSource.None,
        FailureMessage = message
    };
}

public sealed class GeminiAuthResolver
{
    private readonly Func<AntigravityOAuthTokens> _antigravityReader;
    private readonly Func<AntigravityOAuthTokens> _geminiCliReader;

    public GeminiAuthResolver(
        Func<AntigravityOAuthTokens>? antigravityReader = null,
        Func<AntigravityOAuthTokens>? geminiCliReader = null)
    {
        _antigravityReader = antigravityReader ?? AntigravityTokenReader.Read;
        _geminiCliReader = geminiCliReader ?? (() => GeminiCliTokenReader.Read());
    }

    public GeminiAuthResult Resolve()
    {
        var antigravity = _antigravityReader();
        if (HasUsableTokens(antigravity))
            return GeminiAuthResult.FromAntigravity(antigravity);

        var geminiCli = _geminiCliReader();
        if (HasUsableTokens(geminiCli))
            return GeminiAuthResult.FromGeminiCli(geminiCli);

        return GeminiAuthResult.Failed(
            "Sign in to Antigravity IDE or run gemini login (Gemini CLI) on this machine");
    }

    public bool HasDetectableAuth()
    {
        var antigravity = _antigravityReader();
        if (HasUsableTokens(antigravity))
            return true;

        var geminiCli = _geminiCliReader();
        return HasUsableTokens(geminiCli);
    }

    public GeminiAuthSource DetectedSource()
    {
        var antigravity = _antigravityReader();
        if (HasUsableTokens(antigravity))
            return GeminiAuthSource.Antigravity;

        var geminiCli = _geminiCliReader();
        if (HasUsableTokens(geminiCli))
            return GeminiAuthSource.GeminiCli;

        return GeminiAuthSource.None;
    }

    private static bool HasUsableTokens(AntigravityOAuthTokens tokens) =>
        !string.IsNullOrWhiteSpace(tokens.AccessToken) || !string.IsNullOrWhiteSpace(tokens.RefreshToken);
}

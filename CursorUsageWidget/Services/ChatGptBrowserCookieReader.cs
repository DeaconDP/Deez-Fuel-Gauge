namespace CursorUsageWidget.Services;

public sealed class ChatGptBrowserCookieReader
{
    private const string CookieName = "__Secure-next-auth.session-token";

    private static readonly string[] HostPatterns = ["chatgpt.com", ".chatgpt.com"];

    private readonly Func<IReadOnlyList<ChromiumBrowserProfile>> _profileResolver;
    private readonly Func<byte[]?, byte[], string?> _decryptValue;

    public ChatGptBrowserCookieReader(
        Func<IReadOnlyList<ChromiumBrowserProfile>>? profileResolver = null,
        Func<byte[]?, byte[], string?>? decryptValue = null)
    {
        _profileResolver = profileResolver ?? ChromiumCookieHelper.ResolveDefaultProfiles;
        _decryptValue = decryptValue ?? ChromiumCookieHelper.DecryptChromiumCookieValue;
    }

    public string? ReadSessionToken() =>
        ChromiumCookieHelper.TryReadCookie(CookieName, HostPatterns, _profileResolver(), _decryptValue);

    internal static string? TryReadSessionTokenFromCookieDatabase(
        string cookieDatabasePath,
        byte[]? encryptionKey,
        Func<byte[]?, byte[], string?> decryptValue) =>
        ChromiumCookieHelper.TryReadCookieFromDatabase(
            cookieDatabasePath,
            CookieName,
            HostPatterns,
            encryptionKey,
            decryptValue);
}

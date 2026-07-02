namespace CursorUsageWidget.Services;

public sealed class OpenCodeBrowserCookieReader
{
    private const string CookieName = "auth";

    private static readonly string[] HostPatterns = ["opencode.ai", ".opencode.ai"];

    private readonly Func<IReadOnlyList<ChromiumBrowserProfile>> _profileResolver;
    private readonly Func<byte[]?, byte[], string?> _decryptValue;

    public OpenCodeBrowserCookieReader(
        Func<IReadOnlyList<ChromiumBrowserProfile>>? profileResolver = null,
        Func<byte[]?, byte[], string?>? decryptValue = null)
    {
        _profileResolver = profileResolver ?? ChromiumCookieHelper.ResolveDefaultProfiles;
        _decryptValue = decryptValue ?? ChromiumCookieHelper.DecryptChromiumCookieValue;
    }

    public string? ReadAuthCookie() =>
        ChromiumCookieHelper.TryReadCookie(CookieName, HostPatterns, _profileResolver(), _decryptValue);
}

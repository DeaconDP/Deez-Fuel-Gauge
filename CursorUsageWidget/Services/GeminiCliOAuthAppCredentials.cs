using System.Text.RegularExpressions;

namespace CursorUsageWidget.Services;

/// <summary>
/// OAuth app identifiers from the installed Gemini CLI package (not user secrets).
/// Falls back to GEMINI_OAUTH_CLIENT_ID / GEMINI_OAUTH_CLIENT_SECRET environment variables.
/// </summary>
internal static class GeminiCliOAuthAppCredentials
{
    private static readonly object CacheLock = new();
    private static string? _cachedClientId;
    private static string? _cachedClientSecret;
    private static bool _cacheInitialized;

    internal static string? ClientId => ResolveCredentials().ClientId;
    internal static string? ClientSecret => ResolveCredentials().ClientSecret;

    internal static (string? ClientId, string? ClientSecret) ResolveCredentials()
    {
        lock (CacheLock)
        {
            if (_cacheInitialized)
                return (_cachedClientId, _cachedClientSecret);

            _cacheInitialized = true;
            var fromEnvId = Environment.GetEnvironmentVariable("GEMINI_OAUTH_CLIENT_ID");
            var fromEnvSecret = Environment.GetEnvironmentVariable("GEMINI_OAUTH_CLIENT_SECRET");
            if (!string.IsNullOrWhiteSpace(fromEnvId) && !string.IsNullOrWhiteSpace(fromEnvSecret))
            {
                _cachedClientId = fromEnvId.Trim();
                _cachedClientSecret = fromEnvSecret.Trim();
                return (_cachedClientId, _cachedClientSecret);
            }

            foreach (var oauthPath in CandidateOAuthJsPaths())
            {
                if (!File.Exists(oauthPath))
                    continue;

                try
                {
                    var text = File.ReadAllText(oauthPath);
                    var clientId = ExtractConstant(text, "OAUTH_CLIENT_ID");
                    var clientSecret = ExtractConstant(text, "OAUTH_CLIENT_SECRET");
                    if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
                    {
                        _cachedClientId = clientId;
                        _cachedClientSecret = clientSecret;
                        return (_cachedClientId, _cachedClientSecret);
                    }
                }
                catch
                {
                    // try next path
                }
            }

            return (null, null);
        }
    }

    private static IEnumerable<string> CandidateOAuthJsPaths()
    {
        var paths = new List<string>();
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim();
            paths.Add(Path.Combine(trimmed, "node_modules", "@google", "gemini-cli-core", "dist", "src", "code_assist", "oauth2.js"));
            paths.Add(Path.Combine(trimmed, "node_modules", "@google", "gemini-cli", "node_modules", "@google", "gemini-cli-core", "dist", "src", "code_assist", "oauth2.js"));
        }

        if (OperatingSystem.IsMacOS())
        {
            paths.Add("/opt/homebrew/lib/node_modules/@google/gemini-cli/node_modules/@google/gemini-cli-core/dist/src/code_assist/oauth2.js");
            paths.Add("/usr/local/lib/node_modules/@google/gemini-cli/node_modules/@google/gemini-cli-core/dist/src/code_assist/oauth2.js");
        }

        return paths.Distinct();
    }

    private static string? ExtractConstant(string text, string constantName)
    {
        var match = Regex.Match(
            text,
            $@"{constantName}\s*=\s*['""]([^'""]+)['""]",
            RegexOptions.CultureInvariant);
        return match.Success ? match.Groups[1].Value : null;
    }
}

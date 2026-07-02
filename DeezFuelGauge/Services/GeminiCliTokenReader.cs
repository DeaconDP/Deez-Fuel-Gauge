using System.Globalization;
using System.Text.Json;

namespace DeezFuelGauge.Services;

public static class GeminiCliTokenReader
{
    public static AntigravityOAuthTokens Read(string? path = null)
    {
        path ??= PlatformPaths.GeminiOAuthCredentialsPath;
        if (!File.Exists(path))
            return new AntigravityOAuthTokens();

        try
        {
            var json = File.ReadAllText(path);
            return ParseOAuthCredentials(json);
        }
        catch
        {
            return new AntigravityOAuthTokens();
        }
    }

    internal static AntigravityOAuthTokens ParseOAuthCredentials(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new AntigravityOAuthTokens();

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var accessToken = ReadString(root, "access_token", "accessToken");
            var refreshToken = ReadString(root, "refresh_token", "refreshToken");
            var expiresAt = ParseExpiry(root);

            if (string.IsNullOrWhiteSpace(accessToken) && string.IsNullOrWhiteSpace(refreshToken))
                return new AntigravityOAuthTokens();

            return new AntigravityOAuthTokens
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
        }
        catch
        {
            return new AntigravityOAuthTokens();
        }
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static DateTimeOffset? ParseExpiry(JsonElement root)
    {
        if (!root.TryGetProperty("expiry_date", out var expiryEl) && !root.TryGetProperty("expiryDate", out expiryEl))
            return null;

        if (expiryEl.ValueKind == JsonValueKind.Number && expiryEl.TryGetInt64(out var numeric))
            return FromUnixTimestamp(numeric);

        if (expiryEl.ValueKind == JsonValueKind.String
            && long.TryParse(expiryEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return FromUnixTimestamp(parsed);

        return null;
    }

    private static DateTimeOffset FromUnixTimestamp(long value)
    {
        // Gemini CLI stores expiry_date as milliseconds since epoch.
        if (value > 1_000_000_000_000)
            return DateTimeOffset.FromUnixTimeMilliseconds(value);

        return DateTimeOffset.FromUnixTimeSeconds(value);
    }
}

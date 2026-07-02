using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class OpenCodeAuthResolver
{
    private static readonly string[] ApiKeyProviderIds =
    [
        "opencode",
        "opencodezen",
        "opencode-go",
        "opencode-go-plan"
    ];

    private readonly Func<string?> _apiKeyReader;
    private readonly Func<string?> _browserCookieReader;
    private readonly Func<string?, string?> _savedSessionReader;

    public OpenCodeAuthResolver(
        Func<string?>? apiKeyReader = null,
        Func<string?>? browserCookieReader = null,
        Func<string?, string?>? savedSessionReader = null)
    {
        _apiKeyReader = apiKeyReader ?? OpenCodeUsageClient.TryReadLocalApiKey;
        _browserCookieReader = browserCookieReader ?? (() => new OpenCodeBrowserCookieReader().ReadAuthCookie());
        _savedSessionReader = savedSessionReader ?? (id => CredentialStore.Retrieve(id));
    }

    public OpenCodeAuthResult Resolve(ProviderBillingSettings settings, bool tryBrowserCookies = true)
    {
        var apiKey = _apiKeyReader();
        if (!string.IsNullOrWhiteSpace(apiKey))
            return OpenCodeAuthResult.FromApiKey(apiKey);

        if (tryBrowserCookies)
        {
            try
            {
                var browserCookie = _browserCookieReader();
                if (!string.IsNullOrWhiteSpace(browserCookie))
                    return OpenCodeAuthResult.FromBrowserCookie(browserCookie);
            }
            catch (IOException)
            {
                return OpenCodeAuthResult.Failed("Close Chrome or Edge, then click Test again");
            }
            catch (Microsoft.Data.Sqlite.SqliteException)
            {
                return OpenCodeAuthResult.Failed("Close Chrome or Edge, then click Test again");
            }
        }

        var savedSession = _savedSessionReader(settings.ProSessionCredentialId);
        if (!string.IsNullOrWhiteSpace(savedSession))
            return OpenCodeAuthResult.FromSavedSession(savedSession);

        return OpenCodeAuthResult.Failed(
            "OpenCode auth not found — run opencode /connect or paste auth cookie + workspace ID");
    }

    public bool HasDetectableAuth(ProviderBillingSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(_apiKeyReader()))
            return true;

        try
        {
            if (!string.IsNullOrWhiteSpace(_browserCookieReader()))
                return true;
        }
        catch (IOException)
        {
        }
        catch (Microsoft.Data.Sqlite.SqliteException)
        {
        }

        return !string.IsNullOrWhiteSpace(_savedSessionReader(settings.ProSessionCredentialId));
    }

    public bool HasApiKeyAuth() => !string.IsNullOrWhiteSpace(_apiKeyReader());

    public static void PersistBrowserSession(ProviderBillingSettings settings, string sessionCookie)
    {
        CredentialStore.Replace(
            "opencode",
            settings.ProSessionCredentialId,
            sessionCookie,
            id => settings.ProSessionCredentialId = id);
    }

    internal static string? TryReadApiKeyFromJson(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            foreach (var providerId in ApiKeyProviderIds)
            {
                var key = TryReadProviderApiKey(doc.RootElement, providerId);
                if (!string.IsNullOrWhiteSpace(key))
                    return key;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Object)
                    continue;

                var key = TryReadProviderApiKey(doc.RootElement, property.Name);
                if (!string.IsNullOrWhiteSpace(key) &&
                    property.Name.Contains("opencode", StringComparison.OrdinalIgnoreCase))
                    return key;
            }
        }
        catch
        {
        }

        return null;
    }

    private static string? TryReadProviderApiKey(System.Text.Json.JsonElement root, string providerId)
    {
        if (!root.TryGetProperty(providerId, out var provider) ||
            provider.ValueKind != System.Text.Json.JsonValueKind.Object)
            return null;

        if (provider.TryGetProperty("key", out var keyEl) && keyEl.ValueKind == System.Text.Json.JsonValueKind.String)
            return keyEl.GetString();

        if (provider.TryGetProperty("apiKey", out var apiKeyEl) && apiKeyEl.ValueKind == System.Text.Json.JsonValueKind.String)
            return apiKeyEl.GetString();

        return null;
    }
}

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class CodexUsageClient : IDisposable
{
    private const string BaseUrl = "https://chatgpt.com";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Func<string?> _authFilePathResolver;

    public CodexUsageClient(HttpClient? http = null, Func<string?>? authFilePathResolver = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _authFilePathResolver = authFilePathResolver ?? ResolveDefaultAuthFilePath;
    }

    public async Task<CodexSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auth = await ResolveAuthAsync(settings, cancellationToken);
            if (auth is null)
            {
                settings.ProLastConnectionStatus = "Codex auth not found — run codex login or paste session cookie";
                return CodexSnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            var usage = await FetchUsageAsync(auth.Value.AccessToken, auth.Value.AccountId, cancellationToken);
            settings.ProLastConnectionStatus = usage.IsAvailable
                ? $"Connected ({usage.PlanLabel ?? "Codex"})"
                : (usage.StatusMessage ?? "Unavailable");
            return usage;
        }
        catch (CodexUsageException ex)
        {
            settings.ProLastConnectionStatus = ex.Message;
            return CodexSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.ProLastConnectionStatus = "Request failed";
            return CodexSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(
        string? sessionValue = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auth = await ResolveAuthForTestAsync(sessionValue, cancellationToken);
            if (auth is null)
                return "Codex auth not found — run codex login or paste session cookie";

            var usage = await FetchUsageAsync(auth.Value.AccessToken, auth.Value.AccountId, cancellationToken);
            return usage.IsAvailable
                ? $"Connected ({usage.PlanLabel ?? "Codex"})"
                : (usage.StatusMessage ?? "No Codex quota");
        }
        catch (CodexUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static CodexAuth? ParseAuthJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("auth_mode", out var authModeEl)
            && authModeEl.ValueKind == JsonValueKind.String
            && !string.Equals(authModeEl.GetString(), "chatgpt", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!root.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
            return null;

        if (!tokens.TryGetProperty("access_token", out var accessTokenEl)
            || accessTokenEl.ValueKind != JsonValueKind.String)
            return null;

        var accessToken = accessTokenEl.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var accountId = tokens.TryGetProperty("account_id", out var accountIdEl) && accountIdEl.ValueKind == JsonValueKind.String
            ? accountIdEl.GetString()
            : null;

        accountId ??= ExtractAccountIdFromToken(accessToken);
        if (string.IsNullOrWhiteSpace(accountId))
            return null;

        return new CodexAuth(accessToken, accountId);
    }

    internal static CodexSnapshot ParseUsageResponse(JsonElement root, DateTimeOffset? now = null)
    {
        var observedAt = now ?? DateTimeOffset.UtcNow;
        var planType = root.TryGetProperty("plan_type", out var planTypeEl) && planTypeEl.ValueKind == JsonValueKind.String
            ? planTypeEl.GetString()
            : null;

        if (!root.TryGetProperty("rate_limit", out var rateLimit) || rateLimit.ValueKind != JsonValueKind.Object)
            return CodexSnapshot.Unavailable("No Codex quota");

        var limitReached = rateLimit.TryGetProperty("limit_reached", out var limitReachedEl)
                           && limitReachedEl.ValueKind == JsonValueKind.True;

        double? sessionUsed = null;
        double? weeklyUsed = null;
        DateTimeOffset? sessionResetsAt = null;
        DateTimeOffset? weeklyResetsAt = null;

        if (rateLimit.TryGetProperty("primary_window", out var primary) && primary.ValueKind == JsonValueKind.Object)
        {
            sessionUsed = ParseUsedPercent(primary);
            sessionResetsAt = ParseWindowReset(primary, observedAt);
        }

        if (rateLimit.TryGetProperty("secondary_window", out var secondary) && secondary.ValueKind == JsonValueKind.Object)
        {
            weeklyUsed = ParseUsedPercent(secondary);
            weeklyResetsAt = ParseWindowReset(secondary, observedAt);
        }

        if (sessionUsed is null && weeklyUsed is null)
            return CodexSnapshot.Unavailable("No Codex quota");

        var creditsBalance = ParseCreditsBalance(root);

        return CodexSnapshot.FromUsage(
            planType,
            sessionUsed ?? 0,
            weeklyUsed ?? 0,
            sessionResetsAt,
            weeklyResetsAt,
            creditsBalance,
            limitReached);
    }

    internal static string BuildCookieHeader(string sessionValue)
    {
        var trimmed = sessionValue.Trim();
        if (trimmed.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed["Cookie:".Length..].Trim();

        if (trimmed.Contains('=', StringComparison.Ordinal))
            return trimmed;

        return $"__Secure-next-auth.session-token={trimmed}";
    }

    internal static CodexAuth? ParseSessionResponse(JsonElement root)
    {
        if (!root.TryGetProperty("accessToken", out var accessTokenEl) || accessTokenEl.ValueKind != JsonValueKind.String)
            return null;

        var accessToken = accessTokenEl.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        string? accountId = null;
        if (root.TryGetProperty("account", out var account) && account.ValueKind == JsonValueKind.Object)
        {
            if (account.TryGetProperty("id", out var accountIdEl) && accountIdEl.ValueKind == JsonValueKind.String)
                accountId = accountIdEl.GetString();
        }

        accountId ??= ExtractAccountIdFromToken(accessToken);
        if (string.IsNullOrWhiteSpace(accountId))
            return null;

        return new CodexAuth(accessToken, accountId);
    }

    private async Task<CodexAuth?> ResolveAuthAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken)
    {
        var fromFile = TryReadAuthFromFile();
        if (fromFile is not null)
            return fromFile;

        var session = CredentialStore.Retrieve(settings.ProSessionCredentialId);
        if (string.IsNullOrWhiteSpace(session))
            return null;

        return await ExchangeSessionAsync(session, cancellationToken);
    }

    private async Task<CodexAuth?> ResolveAuthForTestAsync(
        string? sessionValue,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(sessionValue))
            return await ExchangeSessionAsync(sessionValue, cancellationToken);

        var fromFile = TryReadAuthFromFile();
        if (fromFile is not null)
            return fromFile;

        return null;
    }

    public bool HasLocalAuthFile() => TryReadAuthFromFile() is not null;

    internal static bool TryReadLocalAuthFile(out CodexAuth? auth, Func<string?>? authFilePathResolver = null)
    {
        auth = TryReadAuthFromPath((authFilePathResolver ?? ResolveDefaultAuthFilePath)());
        return auth is not null;
    }

    private CodexAuth? TryReadAuthFromFile() =>
        TryReadAuthFromPath(_authFilePathResolver());

    internal static CodexAuth? TryReadAuthFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return ParseAuthJson(json);
        }
        catch
        {
            return null;
        }
    }

    internal static string? ResolveDefaultAuthFilePath()
    {
        var codexHome = Environment.GetEnvironmentVariable("CODEX_HOME");
        if (string.IsNullOrWhiteSpace(codexHome))
            codexHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");

        return Path.Combine(codexHome, "auth.json");
    }

    private async Task<CodexAuth?> ExchangeSessionAsync(string sessionValue, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/auth/session");
        request.Headers.TryAddWithoutValidation("Cookie", BuildCookieHeader(sessionValue));
        request.Headers.TryAddWithoutValidation("Referer", "https://chatgpt.com/");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw CodexUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseSessionResponse(document.RootElement);
    }

    private async Task<CodexSnapshot> FetchUsageAsync(
        string accessToken,
        string accountId,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/backend-api/wham/usage");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("chatgpt-account-id", accountId);
        request.Headers.TryAddWithoutValidation("OpenAI-Account-Id", accountId);
        request.Headers.TryAddWithoutValidation("Origin", BaseUrl);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw CodexUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseUsageResponse(document.RootElement);
    }

    private static double? ParseUsedPercent(JsonElement window)
    {
        if (!window.TryGetProperty("used_percent", out var usedEl) || usedEl.ValueKind != JsonValueKind.Number)
            return null;

        return Math.Clamp(usedEl.GetDouble(), 0, 100);
    }

    private static DateTimeOffset? ParseWindowReset(JsonElement window, DateTimeOffset observedAt)
    {
        if (window.TryGetProperty("reset_at", out var resetAtEl))
        {
            if (resetAtEl.ValueKind == JsonValueKind.Number && resetAtEl.TryGetInt64(out var epochSeconds))
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

            if (resetAtEl.ValueKind == JsonValueKind.String)
            {
                var text = resetAtEl.GetString();
                if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed))
                    return parsed;
            }
        }

        if (window.TryGetProperty("reset_after_seconds", out var resetAfterEl)
            && resetAfterEl.ValueKind == JsonValueKind.Number)
            return observedAt.AddSeconds(resetAfterEl.GetDouble());

        return null;
    }

    private static decimal? ParseCreditsBalance(JsonElement root)
    {
        if (!root.TryGetProperty("credits", out var credits) || credits.ValueKind != JsonValueKind.Object)
            return null;

        if (credits.TryGetProperty("balance", out var balanceEl))
        {
            if (balanceEl.ValueKind == JsonValueKind.Number && balanceEl.TryGetDecimal(out var numericBalance))
                return numericBalance;

            if (balanceEl.ValueKind == JsonValueKind.String
                && decimal.TryParse(balanceEl.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedBalance))
                return parsedBalance;
        }

        return null;
    }

    private static string? ExtractAccountIdFromToken(string accessToken)
    {
        try
        {
            var parts = accessToken.Split('.');
            if (parts.Length < 2)
                return null;

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var json = System.Text.Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("chatgpt_account_id", out var accountIdEl) && accountIdEl.ValueKind == JsonValueKind.String)
                return accountIdEl.GetString();

            if (root.TryGetProperty("https://api.openai.com/auth", out var auth)
                && auth.ValueKind == JsonValueKind.Object
                && auth.TryGetProperty("chatgpt_account_id", out var nestedAccountIdEl)
                && nestedAccountIdEl.ValueKind == JsonValueKind.String)
                return nestedAccountIdEl.GetString();
        }
        catch
        {
            // ignore
        }

        return null;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    internal readonly record struct CodexAuth(string AccessToken, string AccountId);
}

internal sealed class CodexUsageException : Exception
{
    public CodexUsageException(string message) : base(message) { }

    public static CodexUsageException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return new CodexUsageException("Session expired — run codex login or paste a new ChatGPT session cookie");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detailEl) && detailEl.ValueKind == JsonValueKind.String)
                return new CodexUsageException(detailEl.GetString() ?? status.ToString());

            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var messageEl))
                return new CodexUsageException(messageEl.GetString() ?? status.ToString());
        }
        catch
        {
            // ignore
        }

        return new CodexUsageException($"HTTP {(int)status}");
    }
}

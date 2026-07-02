using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class CodexUsageClient : IDisposable
{
    private const string BaseUrl = "https://chatgpt.com";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Func<string?> _authFilePathResolver;
    private readonly CodexAuthResolver _authResolver;

    public CodexUsageClient(
        HttpClient? http = null,
        Func<string?>? authFilePathResolver = null,
        CodexAuthResolver? authResolver = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _authFilePathResolver = authFilePathResolver ?? ResolveDefaultAuthFilePath;
        _authResolver = authResolver ?? new CodexAuthResolver(
            authFileReader: () => TryReadAuthFromPath(_authFilePathResolver()));
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
                var resolved = _authResolver.Resolve(settings, tryBrowserCookies: true);
                var message = resolved.FailureMessage
                                ?? "Codex auth not found — run codex login or paste session cookie";
                settings.ProLastConnectionStatus = message;
                return CodexSnapshot.Unavailable(message);
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
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default) =>
        await RefreshAndConnectAsync(settings, cancellationToken);

    public async Task<string> RefreshAndConnectAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var resolved = _authResolver.Resolve(settings, tryBrowserCookies: true);
            if (resolved.Auth is not null)
            {
                var usage = await FetchUsageAsync(
                    resolved.Auth.Value.AccessToken,
                    resolved.Auth.Value.AccountId,
                    cancellationToken);
                settings.ProLastConnectionStatus = usage.IsAvailable
                    ? $"Connected ({usage.PlanLabel ?? "Codex"})"
                    : (usage.StatusMessage ?? "No Codex quota");
                return settings.ProLastConnectionStatus;
            }

            if (!resolved.HasSessionCookie)
                return resolved.FailureMessage ?? "Codex auth not found — run codex login or paste session cookie";

            if (resolved.Source == CodexAuthSource.BrowserCookie)
                CodexAuthResolver.PersistBrowserSession(settings, resolved.SessionCookie!);

            var auth = await ExchangeSessionAsync(resolved.SessionCookie!, cancellationToken);
            if (auth is null)
                return "Session expired — run codex login or paste a new ChatGPT session cookie";

            var sessionUsage = await FetchUsageAsync(auth.Value.AccessToken, auth.Value.AccountId, cancellationToken);
            settings.ProLastConnectionStatus = sessionUsage.IsAvailable
                ? $"Connected ({sessionUsage.PlanLabel ?? "Codex"})"
                : (sessionUsage.StatusMessage ?? "No Codex quota");
            return settings.ProLastConnectionStatus;
        }
        catch (CodexUsageException ex)
        {
            settings.ProLastConnectionStatus = ex.Message;
            return ex.Message;
        }
        catch (Exception)
        {
            settings.ProLastConnectionStatus = "Request failed";
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

        var (sessionUsed, weeklyUsed, sessionResetsAt, weeklyResetsAt, limitReached) = ParseRateLimitWindows(root, observedAt);
        if (sessionUsed is null && weeklyUsed is null)
            return CodexSnapshot.Unavailable("No Codex quota");

        var creditsBalance = ParseCreditsBalance(root);

        return CodexSnapshot.FromUsage(
            planType,
            sessionUsed,
            weeklyUsed,
            sessionResetsAt,
            weeklyResetsAt,
            creditsBalance,
            limitReached);
    }

    internal static (double? SessionUsed, double? WeeklyUsed, DateTimeOffset? SessionResetsAt, DateTimeOffset? WeeklyResetsAt, bool LimitReached)
        ParseRateLimitWindows(JsonElement root, DateTimeOffset observedAt)
    {
        var limitReached = false;
        JsonElement? rateLimitContainer = null;

        if (root.TryGetProperty("rate_limit", out var rateLimit) && rateLimit.ValueKind == JsonValueKind.Object)
        {
            rateLimitContainer = rateLimit;
            limitReached = rateLimit.TryGetProperty("limit_reached", out var limitReachedEl)
                           && limitReachedEl.ValueKind == JsonValueKind.True;
        }

        var sessionCandidate = FindWindow(
            root,
            rateLimitContainer,
            "five_hour",
            "five_hour_limit",
            "five_hour_rate_limit");
        var weeklyCandidate = FindWindow(
            root,
            rateLimitContainer,
            "weekly",
            "weekly_limit",
            "weekly_rate_limit");
        var primaryCandidate = FindWindow(root, rateLimitContainer, "primary", "primary_window");
        var secondaryCandidate = FindWindow(root, rateLimitContainer, "secondary", "secondary_window");

        sessionCandidate ??= primaryCandidate;
        weeklyCandidate ??= secondaryCandidate;

        var (sessionWindow, weeklyWindow) = ClassifyRateLimitWindows(
            sessionCandidate,
            weeklyCandidate,
            primaryCandidate,
            secondaryCandidate);

        double? sessionUsed = null;
        double? weeklyUsed = null;
        DateTimeOffset? sessionResetsAt = null;
        DateTimeOffset? weeklyResetsAt = null;

        if (sessionWindow is { } session)
        {
            sessionUsed = ParseUsedPercent(session);
            sessionResetsAt = ParseWindowReset(session, observedAt);
        }

        if (weeklyWindow is { } weekly)
        {
            weeklyUsed = ParseUsedPercent(weekly);
            weeklyResetsAt = ParseWindowReset(weekly, observedAt);
        }

        return (sessionUsed, weeklyUsed, sessionResetsAt, weeklyResetsAt, limitReached);
    }

    internal static (JsonElement? Session, JsonElement? Weekly) ClassifyRateLimitWindows(
        JsonElement? sessionCandidate,
        JsonElement? weeklyCandidate,
        JsonElement? primaryCandidate,
        JsonElement? secondaryCandidate)
    {
        var primaryRole = InferWindowRole(primaryCandidate, defaultRole: "session");
        var secondaryRole = InferWindowRole(secondaryCandidate, defaultRole: "weekly");
        var sessionRole = InferWindowRole(sessionCandidate, defaultRole: "session");
        var weeklyRole = InferWindowRole(weeklyCandidate, defaultRole: "weekly");

        JsonElement? sessionWindow = null;
        JsonElement? weeklyWindow = null;

        if (sessionCandidate is { } explicitSession && sessionRole == "session")
            sessionWindow = UnwrapRateLimitWindow(explicitSession);
        if (weeklyCandidate is { } explicitWeekly && weeklyRole == "weekly")
            weeklyWindow = UnwrapRateLimitWindow(explicitWeekly);

        if (sessionWindow is null && primaryCandidate is { } primary)
        {
            if (primaryRole == "session")
                sessionWindow = UnwrapRateLimitWindow(primary);
            else if (primaryRole == "weekly")
                weeklyWindow = UnwrapRateLimitWindow(primary);
        }

        if (weeklyWindow is null && secondaryCandidate is { } secondary)
        {
            if (secondaryRole == "weekly")
                weeklyWindow = UnwrapRateLimitWindow(secondary);
            else if (secondaryRole == "session")
                sessionWindow ??= UnwrapRateLimitWindow(secondary);
        }

        if (sessionWindow is not null && weeklyWindow is not null)
            return (sessionWindow, weeklyWindow);

        if (sessionWindow is null && weeklyWindow is null && primaryCandidate is { } onlyPrimary)
        {
            if (primaryRole == "weekly")
                return (null, UnwrapRateLimitWindow(onlyPrimary));

            return (UnwrapRateLimitWindow(onlyPrimary), null);
        }

        return (sessionWindow, weeklyWindow);
    }

    internal static string InferWindowRole(JsonElement? window, string defaultRole)
    {
        if (window is not { } element)
            return defaultRole;

        var unwrapped = UnwrapRateLimitWindow(element);
        if (!unwrapped.TryGetProperty("limit_window_seconds", out var secondsEl)
            || secondsEl.ValueKind != JsonValueKind.Number)
            return defaultRole;

        var seconds = secondsEl.GetDouble();
        if (seconds <= 6 * 3600)
            return "session";
        if (seconds >= 6 * 24 * 3600)
            return "weekly";

        return defaultRole;
    }

    internal static JsonElement UnwrapRateLimitWindow(JsonElement window)
    {
        if (!window.TryGetProperty("reset_at", out _)
            && !window.TryGetProperty("reset_after_seconds", out _)
            && window.TryGetProperty("primary_window", out var nested)
            && nested.ValueKind == JsonValueKind.Object)
            return nested;

        return window;
    }

    internal static JsonElement? FindWindow(
        JsonElement root,
        JsonElement? rateLimitContainer,
        params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (rateLimitContainer is { } container
                && container.TryGetProperty(name, out var fromRateLimit)
                && fromRateLimit.ValueKind == JsonValueKind.Object)
                return fromRateLimit;

            if (root.TryGetProperty(name, out var fromRoot) && fromRoot.ValueKind == JsonValueKind.Object)
                return fromRoot;
        }

        return null;
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
        var resolved = _authResolver.Resolve(settings, tryBrowserCookies: true);
        if (resolved.Auth is not null)
            return resolved.Auth;

        if (!resolved.HasSessionCookie)
            return null;

        var auth = await ExchangeSessionAsync(resolved.SessionCookie!, cancellationToken);
        if (auth is not null && resolved.Source == CodexAuthSource.BrowserCookie)
            CodexAuthResolver.PersistBrowserSession(settings, resolved.SessionCookie!);

        return auth;
    }

    public bool HasLocalAuthFile() => TryReadAuthFromFile() is not null;

    public bool HasDetectableAuth(ProviderBillingSettings settings) =>
        _authResolver.HasDetectableAuth(settings);

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
        request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", accountId);
        request.Headers.TryAddWithoutValidation("OpenAI-Account-Id", accountId);
        request.Headers.TryAddWithoutValidation("Origin", BaseUrl);
        request.Headers.TryAddWithoutValidation("Referer", $"{BaseUrl}/");
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

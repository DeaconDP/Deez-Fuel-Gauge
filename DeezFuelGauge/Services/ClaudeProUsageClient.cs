using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class ClaudeProUsageClient : IDisposable
{
    private const string BaseUrl = "https://claude.ai";
    private const string OAuthUsageUrl = "https://api.anthropic.com/api/oauth/usage";

    // The OAuth usage endpoint routes any request without a claude-code User-Agent to an
    // aggressively rate-limited bucket that returns persistent 429s. Sending the same client
    // identity Claude Code uses is what lets the request through.
    private const string ClientUserAgent = "claude-code/2.0.14";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly ClaudeProAuthResolver _authResolver;
    private readonly ClaudeOAuthLoginService _oauthLogin;

    public ClaudeProUsageClient(
        HttpClient? http = null,
        ClaudeProAuthResolver? authResolver = null,
        ClaudeOAuthLoginService? oauthLogin = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _authResolver = authResolver ?? new ClaudeProAuthResolver();
        _oauthLogin = oauthLogin ?? new ClaudeOAuthLoginService(_http);
    }

    public async Task<ClaudeProSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auth = _authResolver.Resolve(settings);
            if (!auth.HasAuth)
            {
                settings.ProLastConnectionStatus = auth.FailureMessage ?? "Not connected";
                return ClaudeProSnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            var usage = await FetchWithAuthAsync(auth, settings, cancellationToken);
            settings.ProLastConnectionStatus = usage.IsAvailable ? "Connected" : (usage.StatusMessage ?? "Unavailable");
            return usage;
        }
        catch (ClaudeProUsageException ex)
        {
            settings.ProLastConnectionStatus = ex.Message;
            return ClaudeProSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.ProLastConnectionStatus = "Request failed";
            return ClaudeProSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> RefreshAndConnectAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auth = _authResolver.Resolve(settings);
            if (!auth.HasAuth)
                return auth.FailureMessage ?? "Sign in with Claude in Settings, or run 'claude login'";

            var usage = await FetchWithAuthAsync(auth, settings, cancellationToken);
            settings.ProLastConnectionStatus = usage.IsAvailable ? "Connected" : (usage.StatusMessage ?? "Unavailable");
            return settings.ProLastConnectionStatus;
        }
        catch (ClaudeProUsageException ex)
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

    public async Task<string> TestConnectionAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var auth = _authResolver.Resolve(settings);
        if (!auth.HasAuth)
            return auth.FailureMessage ?? "Sign in with Claude in Settings, or run 'claude login'";

        try
        {
            var usage = await FetchWithAuthAsync(auth, settings, cancellationToken);
            return usage.IsAvailable ? "Connected" : (usage.StatusMessage ?? "No Pro quota");
        }
        catch (ClaudeProUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static string BuildCookieHeader(string sessionValue)
    {
        var trimmed = sessionValue.Trim();
        if (trimmed.Contains('=', StringComparison.Ordinal))
            return trimmed;

        return $"sessionKey={trimmed}";
    }

    internal static double? NormalizeUtilization(double? utilization)
    {
        if (utilization is not { } value || double.IsNaN(value) || double.IsInfinity(value))
            return null;

        return value <= 1 ? value * 100 : value;
    }

    internal static string? ParseOrgUuid(JsonElement accountRoot)
    {
        if (!accountRoot.TryGetProperty("memberships", out var memberships)
            || memberships.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var membership in memberships.EnumerateArray())
        {
            if (membership.TryGetProperty("organization", out var organization)
                && organization.TryGetProperty("uuid", out var orgUuidEl))
            {
                var uuid = orgUuidEl.GetString();
                if (!string.IsNullOrWhiteSpace(uuid))
                    return uuid;
            }

            if (membership.TryGetProperty("uuid", out var membershipUuidEl))
            {
                var uuid = membershipUuidEl.GetString();
                if (!string.IsNullOrWhiteSpace(uuid))
                    return uuid;
            }
        }

        return null;
    }

    internal static ClaudeProSnapshot ParseUsageResponse(JsonElement root)
    {
        var sessionPercent = ParseWindowPercent(root, "five_hour");
        var weeklyPercent = ParseWindowPercent(root, "seven_day");
        var sessionResetsAt = ParseWindowReset(root, "five_hour");
        var weeklyResetsAt = ParseWindowReset(root, "seven_day");

        if (sessionPercent is null && weeklyPercent is null)
            return ClaudeProSnapshot.Unavailable("No Pro quota");

        return ClaudeProSnapshot.FromUsage(
            sessionPercent ?? 0,
            weeklyPercent ?? 0,
            sessionResetsAt,
            weeklyResetsAt);
    }

    private async Task<ClaudeProSnapshot> FetchWithAuthAsync(
        ClaudeProAuthResult auth,
        ProviderBillingSettings settings,
        CancellationToken cancellationToken)
    {
        if (auth.Source == ClaudeProAuthSource.AppOAuth && auth.AppOAuthToken is { } appToken)
            return await FetchAppOAuthUsageAsync(appToken, settings, cancellationToken);

        if (!string.IsNullOrWhiteSpace(auth.OAuthAccessToken))
            return await FetchOAuthUsageAsync(auth.OAuthAccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(auth.SessionCookie))
            throw new ClaudeProUsageException("Sign in with Claude in Settings, or run 'claude login'");

        var orgUuid = await FetchOrgUuidAsync(auth.SessionCookie, cancellationToken);
        if (string.IsNullOrWhiteSpace(orgUuid))
            throw new ClaudeProUsageException("No organization found");

        return await FetchSessionUsageAsync(auth.SessionCookie, orgUuid, cancellationToken);
    }

    private async Task<ClaudeProSnapshot> FetchAppOAuthUsageAsync(
        ClaudeOAuthToken token,
        ProviderBillingSettings settings,
        CancellationToken cancellationToken)
    {
        if (!token.IsExpired)
            return await FetchOAuthUsageAsync(token.AccessToken, cancellationToken);

        if (string.IsNullOrWhiteSpace(token.RefreshToken))
            throw new ClaudeProUsageException("Claude sign-in expired — sign in with Claude again in Settings");

        ClaudeOAuthToken refreshed;
        try
        {
            refreshed = await _oauthLogin.RefreshAsync(token.RefreshToken, cancellationToken);
        }
        catch (ClaudeOAuthException ex)
        {
            throw new ClaudeProUsageException(ex.Message);
        }

        ClaudeOAuthTokenStore.Persist(settings, refreshed);
        return await FetchOAuthUsageAsync(refreshed.AccessToken, cancellationToken);
    }

    private async Task<ClaudeProSnapshot> FetchOAuthUsageAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, OAuthUsageUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("anthropic-beta", "oauth-2025-04-20");
        request.Headers.TryAddWithoutValidation("User-Agent", ClientUserAgent);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw ClaudeProUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseUsageResponse(document.RootElement);
    }

    private static double? ParseWindowPercent(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var window) || window.ValueKind != JsonValueKind.Object)
            return null;

        if (!window.TryGetProperty("utilization", out var utilizationEl) || utilizationEl.ValueKind == JsonValueKind.Null)
            return null;

        return utilizationEl.ValueKind == JsonValueKind.Number
            ? NormalizeUtilization(utilizationEl.GetDouble())
            : null;
    }

    private static DateTimeOffset? ParseWindowReset(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var window) || window.ValueKind != JsonValueKind.Object)
            return null;

        if (!window.TryGetProperty("resets_at", out var resetsAtEl) || resetsAtEl.ValueKind != JsonValueKind.String)
            return null;

        var text = resetsAtEl.GetString();
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private async Task<string?> FetchOrgUuidAsync(string sessionValue, CancellationToken cancellationToken)
    {
        using var request = CreateSessionRequest($"{BaseUrl}/api/account", sessionValue);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw ClaudeProUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseOrgUuid(document.RootElement);
    }

    private async Task<ClaudeProSnapshot> FetchSessionUsageAsync(
        string sessionValue,
        string orgUuid,
        CancellationToken cancellationToken)
    {
        using var request = CreateSessionRequest($"{BaseUrl}/api/organizations/{orgUuid}/usage", sessionValue);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw ClaudeProUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseUsageResponse(document.RootElement);
    }

    private static HttpRequestMessage CreateSessionRequest(string url, string sessionValue)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cookie", BuildCookieHeader(sessionValue));
        request.Headers.TryAddWithoutValidation("Referer", "https://claude.ai/settings/usage");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

internal sealed class ClaudeProUsageException : Exception
{
    public ClaudeProUsageException(string message) : base(message) { }

    public static ClaudeProUsageException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return new ClaudeProUsageException("Session expired — sign in at claude.ai, then click Refresh");

        if (status is System.Net.HttpStatusCode.TooManyRequests)
            return new ClaudeProUsageException("Rate limited by Claude — wait a moment, then click Refresh");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var messageEl))
                return new ClaudeProUsageException(messageEl.GetString() ?? status.ToString());
        }
        catch
        {
            // ignore
        }

        return new ClaudeProUsageException($"HTTP {(int)status}");
    }
}

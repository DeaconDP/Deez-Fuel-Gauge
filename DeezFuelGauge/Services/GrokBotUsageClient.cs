using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class GrokBotUsageClient : IDisposable
{
    private const string ApiBase = "https://api2.cursor.sh";
    private const string OAuthClientId = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB";

    private readonly HttpClient _http;
    private readonly Func<CursorTokens> _tokenReader;
    private string? _accessToken;
    private string? _refreshToken;

    public GrokBotUsageClient(HttpClient? http = null, Func<CursorTokens>? tokenReader = null)
    {
        _http = http ?? new HttpClient();
        _tokenReader = tokenReader ?? CursorTokenReader.Read;
    }

    public void SetTokens(string? accessToken, string? refreshToken)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
    }

    public async Task<GrokBotSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureTokensFromCursor();

        if (string.IsNullOrWhiteSpace(_accessToken))
        {
            const string message = "Sign in to Cursor";
            settings.LastConnectionStatus = message;
            return GrokBotSnapshot.Unavailable(message);
        }

        if (IsJwtExpired(_accessToken))
        {
            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (!refreshed)
            {
                const string message = "Session expired — reopen Cursor";
                settings.LastConnectionStatus = message;
                return GrokBotSnapshot.Unavailable(message);
            }
        }

        try
        {
            using var request = CreateAuthorizedRequest(
                HttpMethod.Post,
                $"{ApiBase}/aiserver.v1.DashboardService/GetSandUsageStatus",
                "{}");
            request.Headers.Add("Connect-Protocol-Version", "1");

            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var message = $"HTTP {(int)response.StatusCode}";
                settings.LastConnectionStatus = message;
                return GrokBotSnapshot.Unavailable(message);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var snapshot = ParseSandUsageStatus(document.RootElement);
            settings.LastConnectionStatus = snapshot.IsAvailable
                ? "Connected"
                : snapshot.StatusMessage ?? "Unavailable";
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            const string message = "Request failed";
            settings.LastConnectionStatus = message;
            return GrokBotSnapshot.Unavailable(message);
        }
    }

    public async Task<string> TestConnectionAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await FetchAsync(settings, cancellationToken);
        return settings.LastConnectionStatus
               ?? (snapshot.IsAvailable ? "Connected" : snapshot.StatusMessage ?? "Unavailable");
    }

    internal static GrokBotSnapshot ParseSandUsageStatus(JsonElement root)
    {
        if (root.TryGetProperty("usesPooledEnterpriseAllowance", out var pooled)
            && pooled.ValueKind == JsonValueKind.True)
        {
            return GrokBotSnapshot.Unavailable("Pooled enterprise allowance — no personal weekly meter");
        }

        if (!TryGetFinitePercent(root, "usagePercent", out var usagePercent)
            && !TryGetFinitePercent(root, "usage_percent", out usagePercent))
        {
            return GrokBotSnapshot.Unavailable("No Grok Bot usage");
        }

        var periodStart = TryParseTimestamp(root, "currentPeriodStart")
                          ?? TryParseTimestamp(root, "current_period_start");
        var resetsAt = TryParseTimestamp(root, "nextResetTimestampUtc")
                       ?? TryParseTimestamp(root, "next_reset_timestamp_utc");
        var hasAvailableUsage = TryGetBoolean(root, "hasAvailableUsage")
                                ?? TryGetBoolean(root, "has_available_usage")
                                ?? true;
        var hasNonZeroIncludedLimit = TryGetBoolean(root, "hasNonZeroIncludedLimit")
                                      ?? TryGetBoolean(root, "has_non_zero_included_limit")
                                      ?? true;

        return GrokBotSnapshot.FromUsage(
            usagePercent,
            periodStart,
            resetsAt,
            hasAvailableUsage,
            hasNonZeroIncludedLimit);
    }

    private void EnsureTokensFromCursor()
    {
        if (!string.IsNullOrWhiteSpace(_accessToken))
            return;

        var tokens = _tokenReader();
        _accessToken = tokens.AccessToken;
        _refreshToken = tokens.RefreshToken;
    }

    private async Task<bool> TryRefreshTokenAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_refreshToken))
            return false;

        var body = JsonSerializer.Serialize(new
        {
            grant_type = "refresh_token",
            client_id = OAuthClientId,
            refresh_token = _refreshToken
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{ApiBase}/oauth/token")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return false;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("shouldLogout", out var logoutEl) && logoutEl.GetBoolean())
            return false;

        if (!document.RootElement.TryGetProperty("access_token", out var tokenEl))
            return false;

        var newToken = tokenEl.GetString();
        if (string.IsNullOrWhiteSpace(newToken))
            return false;

        _accessToken = newToken;
        return true;
    }

    private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string url, string? jsonBody = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

        if (jsonBody is not null)
            request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return request;
    }

    private static bool TryGetFinitePercent(JsonElement parent, string propertyName, out double value)
    {
        value = 0;
        if (!parent.TryGetProperty(propertyName, out var element))
            return false;

        if (element.ValueKind == JsonValueKind.Number)
        {
            value = element.GetDouble();
            return double.IsFinite(value);
        }

        if (element.ValueKind == JsonValueKind.String
            && double.TryParse(
                element.GetString(),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value))
            return double.IsFinite(value);

        return false;
    }

    private static bool? TryGetBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
            return null;

        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static DateTimeOffset? TryParseTimestamp(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
            return null;

        if (element.ValueKind == JsonValueKind.String)
        {
            var text = element.GetString();
            if (string.IsNullOrWhiteSpace(text))
                return null;

            if (DateTimeOffset.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var iso))
                return iso;

            if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var msFromString))
                return DateTimeOffset.FromUnixTimeMilliseconds(NormalizeUnixTimestampMs(msFromString));
        }

        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var numeric))
            return DateTimeOffset.FromUnixTimeMilliseconds(NormalizeUnixTimestampMs(numeric));

        if (element.ValueKind == JsonValueKind.Object)
        {
            // Protobuf Timestamp JSON: { "seconds": "...", "nanos": ... }
            if (element.TryGetProperty("seconds", out var secondsEl))
            {
                long seconds = 0;
                if (secondsEl.ValueKind == JsonValueKind.Number)
                    seconds = secondsEl.GetInt64();
                else if (secondsEl.ValueKind == JsonValueKind.String
                         && long.TryParse(secondsEl.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    seconds = parsed;
                else
                    return null;

                var nanos = 0;
                if (element.TryGetProperty("nanos", out var nanosEl) && nanosEl.ValueKind == JsonValueKind.Number)
                    nanos = nanosEl.GetInt32();

                return DateTimeOffset.FromUnixTimeSeconds(seconds).AddTicks(nanos / 100);
            }
        }

        return null;
    }

    private static long NormalizeUnixTimestampMs(long value) =>
        value > 1_000_000_000_000 ? value : value * 1000;

    private static bool IsJwtExpired(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
                return true;

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));
            using var document = JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty("exp", out var expEl))
                return false;

            var exp = DateTimeOffset.FromUnixTimeSeconds(expEl.GetInt64());
            return exp <= DateTimeOffset.UtcNow.AddMinutes(1);
        }
        catch
        {
            return true;
        }
    }

    public void Dispose() => _http.Dispose();
}

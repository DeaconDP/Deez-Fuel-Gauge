using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class UsageClient : IDisposable
{
    private const string ApiBase = "https://api2.cursor.sh";
    private const string OAuthClientId = "KbZUR41cY7W6zRSdpSUJ7I7mLYBKOCmB";
    private const string IncludedModelKey = "gpt-4";

    private readonly HttpClient _http = new();
    private string? _accessToken;
    private string? _refreshToken;

    public void SetTokens(string? accessToken, string? refreshToken)
    {
        _accessToken = accessToken;
        _refreshToken = refreshToken;
    }

    public async Task<UsageSnapshot> FetchAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accessToken))
            return UsageSnapshot.Error("Sign in to Cursor");

        if (IsJwtExpired(_accessToken))
        {
            var refreshed = await TryRefreshTokenAsync(cancellationToken);
            if (!refreshed)
                return UsageSnapshot.Error("Session expired — reopen Cursor");
        }

        var periodUsage = await TryGetCurrentPeriodUsageAsync(cancellationToken);
        if (periodUsage is not null)
            return periodUsage;

        var legacyUsage = await TryGetLegacyUsageAsync(cancellationToken);
        if (legacyUsage is not null)
            return legacyUsage;

        return UsageSnapshot.Error("Can't fetch usage");
    }

    private async Task<UsageSnapshot?> TryGetCurrentPeriodUsageAsync(CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"{ApiBase}/aiserver.v1.DashboardService/GetCurrentPeriodUsage",
            "{}");

        request.Headers.Add("Connect-Protocol-Version", "1");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("planUsage", out var planUsage))
            return null;

        if (!planUsage.TryGetProperty("limit", out var limitElement))
            return null;

        var limit = limitElement.GetInt64();
        if (limit <= 0)
            return null;

        var percent = GetFinitePercent(planUsage, "totalPercentUsed");
        if (percent is null)
        {
            var includedSpend = planUsage.TryGetProperty("includedSpend", out var spendEl)
                ? spendEl.GetInt64()
                : 0;
            percent = limit > 0 ? includedSpend * 100.0 / limit : 0;
        }

        var remaining = planUsage.TryGetProperty("remaining", out var remainingEl)
            ? remainingEl.GetInt64()
            : Math.Max(0, limit - (planUsage.TryGetProperty("includedSpend", out var inc) ? inc.GetInt64() : 0));

        return new UsageSnapshot
        {
            PercentUsed = Math.Clamp(percent.Value, 0, 100),
            RemainingLabel = $"${remaining / 100.0:F2} left"
        };
    }

    private async Task<UsageSnapshot?> TryGetLegacyUsageAsync(CancellationToken cancellationToken)
    {
        using var request = CreateAuthorizedRequest(HttpMethod.Get, $"{ApiBase}/auth/usage");
        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty(IncludedModelKey, out var bucket))
            return null;

        if (!bucket.TryGetProperty("maxRequestUsage", out var maxEl) || maxEl.ValueKind == JsonValueKind.Null)
            return null;

        var maxRequests = maxEl.GetInt32();
        if (maxRequests <= 0)
            return null;

        var used = bucket.TryGetProperty("numRequests", out var usedEl) ? usedEl.GetInt32() : 0;
        var remaining = Math.Max(0, maxRequests - used);
        var percent = used * 100.0 / maxRequests;

        return new UsageSnapshot
        {
            PercentUsed = Math.Clamp(percent, 0, 100),
            RemainingLabel = $"{remaining} requests left"
        };
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

    private static double? GetFinitePercent(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element))
            return null;

        if (element.ValueKind != JsonValueKind.Number)
            return null;

        var value = element.GetDouble();
        return double.IsFinite(value) ? value : null;
    }

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
            return false;
        }
    }

    public void Dispose() => _http.Dispose();
}

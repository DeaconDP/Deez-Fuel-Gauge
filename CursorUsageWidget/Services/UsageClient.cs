using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class UsageClient : IDisposable
{
    private const string ApiBase = "https://api2.cursor.sh";
    private const string DashboardBase = "https://cursor.com";
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
            return await EnrichWithProviderBreakdownAsync(periodUsage, cancellationToken);

        var legacyUsage = await TryGetLegacyUsageAsync(cancellationToken);
        if (legacyUsage is not null)
            return legacyUsage;

        return UsageSnapshot.Error("Can't fetch usage");
    }

    private async Task<UsageSnapshot> EnrichWithProviderBreakdownAsync(
        UsageSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        if (snapshot.IsError || string.IsNullOrWhiteSpace(_accessToken))
            return snapshot;

        var breakdown = await TryGetProviderBreakdownAsync(snapshot, cancellationToken);
        if (breakdown is null)
            return snapshot;

        return new UsageSnapshot
        {
            PercentUsed = snapshot.PercentUsed,
            RemainingLabel = snapshot.RemainingLabel,
            AutoPercentUsed = snapshot.AutoPercentUsed,
            ApiPercentUsed = snapshot.ApiPercentUsed,
            PlanLimitCents = snapshot.PlanLimitCents,
            BillingCycleStartMs = snapshot.BillingCycleStartMs,
            BillingCycleEndMs = snapshot.BillingCycleEndMs,
            OpenAi = breakdown.Value.OpenAi,
            Gemini = breakdown.Value.Gemini,
            IsError = snapshot.IsError,
            ErrorMessage = snapshot.ErrorMessage
        };
    }

    private async Task<(ProviderUsageSnapshot OpenAi, ProviderUsageSnapshot Gemini)?>
        TryGetProviderBreakdownAsync(UsageSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.PlanLimitCents is not > 0
            || snapshot.BillingCycleStartMs is null
            || snapshot.BillingCycleEndMs is null)
            return null;

        var aggregations = await TryGetAggregatedUsageAsync(
            snapshot.BillingCycleStartMs.Value,
            snapshot.BillingCycleEndMs.Value,
            cancellationToken);

        if (aggregations is null)
            return null;

        var spendByProvider = new Dictionary<ModelProvider, double>
        {
            [ModelProvider.OpenAi] = 0,
            [ModelProvider.Gemini] = 0
        };

        foreach (var (modelName, cents) in aggregations)
        {
            var provider = ModelProviderClassifier.Classify(modelName);
            if (provider is ModelProvider.Unknown)
                continue;

            spendByProvider[provider] += cents;
        }

        var limit = snapshot.PlanLimitCents.Value;
        return (
            ProviderUsageSnapshot.FromSpend(spendByProvider[ModelProvider.OpenAi], limit),
            ProviderUsageSnapshot.FromSpend(spendByProvider[ModelProvider.Gemini], limit));
    }

    private async Task<IReadOnlyList<(string ModelName, double Cents)>?> TryGetAggregatedUsageAsync(
        long startMs,
        long endMs,
        CancellationToken cancellationToken)
    {
        var userId = JwtHelper.GetSessionUserId(_accessToken!);
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        var body = JsonSerializer.Serialize(new
        {
            teamId = -1,
            startDate = startMs.ToString(),
            endDate = endMs.ToString()
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{DashboardBase}/api/dashboard/get-aggregated-usage-events")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        request.Headers.Add("Origin", DashboardBase);
        request.Headers.Add(
            "Cookie",
            $"WorkosCursorSessionToken={Uri.EscapeDataString(userId)}%3A%3A{Uri.EscapeDataString(_accessToken!)}");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("aggregations", out var aggregationsEl)
            || aggregationsEl.ValueKind != JsonValueKind.Array)
            return null;

        var results = new List<(string ModelName, double Cents)>();
        foreach (var item in aggregationsEl.EnumerateArray())
        {
            if (!item.TryGetProperty("modelIntent", out var modelEl))
                continue;

            var modelName = modelEl.GetString();
            if (string.IsNullOrWhiteSpace(modelName))
                continue;

            if (!item.TryGetProperty("totalCents", out var centsEl) || centsEl.ValueKind == JsonValueKind.Null)
                continue;

            var cents = centsEl.GetDouble();
            if (!double.IsFinite(cents) || cents <= 0)
                continue;

            results.Add((modelName, cents));
        }

        return results;
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

        return UsageResponseParser.ParseCurrentPeriodUsage(document.RootElement);
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

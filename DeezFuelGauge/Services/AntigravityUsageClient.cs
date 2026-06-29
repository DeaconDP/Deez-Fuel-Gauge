using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class AntigravityUsageClient : IDisposable
{
    private const string CloudCodeBaseUrl = "https://cloudcode-pa.googleapis.com";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly Func<AntigravityOAuthTokens> _tokenReader;

    public AntigravityUsageClient(HttpClient? http = null, Func<AntigravityOAuthTokens>? tokenReader = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _tokenReader = tokenReader ?? AntigravityTokenReader.Read;
    }

    public async Task<AntigravitySnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await ResolveAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                settings.ProLastConnectionStatus = "Sign in to Antigravity on this machine";
                return AntigravitySnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            var (projectId, planLabel) = await LoadProjectInfoAsync(accessToken, cancellationToken);
            var summary = await FetchQuotaSummaryAsync(accessToken, projectId, cancellationToken);
            var snapshot = ParseQuotaSummary(summary, planLabel);
            settings.ProLastConnectionStatus = snapshot.IsAvailable
                ? $"Connected ({snapshot.PlanLabel ?? "Antigravity"})"
                : (snapshot.StatusMessage ?? "No Antigravity quota");
            return snapshot;
        }
        catch (AntigravityUsageException ex)
        {
            settings.ProLastConnectionStatus = ex.Message;
            return AntigravitySnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.ProLastConnectionStatus = "Request failed";
            return AntigravitySnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var accessToken = await ResolveAccessTokenAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
                return "Sign in to Antigravity on this machine";

            var (projectId, planLabel) = await LoadProjectInfoAsync(accessToken, cancellationToken);
            var summary = await FetchQuotaSummaryAsync(accessToken, projectId, cancellationToken);
            var snapshot = ParseQuotaSummary(summary, planLabel);
            return snapshot.IsAvailable
                ? $"Connected ({snapshot.PlanLabel ?? "Antigravity"})"
                : (snapshot.StatusMessage ?? "No Antigravity quota");
        }
        catch (AntigravityUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static AntigravitySnapshot ParseQuotaSummary(JsonElement root, string? planLabel = null)
    {
        if (!root.TryGetProperty("quota_groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
            return AntigravitySnapshot.Unavailable("No Antigravity quota");

        AntigravityGroupSnapshot? gemini = null;
        AntigravityGroupSnapshot? thirdParty = null;

        foreach (var group in groups.EnumerateArray())
        {
            var displayName = ReadStringProperty(group, "display_name", "displayName") ?? "";
            var parsed = ParseQuotaGroup(group);
            if (IsGeminiGroup(displayName))
                gemini = parsed;
            else if (IsThirdPartyGroup(displayName))
                thirdParty = parsed;
        }

        return AntigravitySnapshot.FromGroups(
            planLabel,
            gemini ?? AntigravityGroupSnapshot.Unavailable(),
            thirdParty ?? AntigravityGroupSnapshot.Unavailable());
    }

    internal static AntigravityGroupSnapshot ParseQuotaGroup(JsonElement group)
    {
        if (!group.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            return AntigravityGroupSnapshot.Unavailable("No quota buckets");

        double? sessionRemaining = null;
        double? weeklyRemaining = null;
        DateTimeOffset? sessionResetsAt = null;
        DateTimeOffset? weeklyResetsAt = null;

        foreach (var bucket in buckets.EnumerateArray())
        {
            var window = ReadStringProperty(bucket, "window") ?? "";
            var remaining = ParseRemainingFraction(bucket);
            var resetAt = ParseResetTime(bucket);

            if (window.Equals("5h", StringComparison.OrdinalIgnoreCase))
            {
                sessionRemaining = remaining;
                sessionResetsAt = resetAt;
            }
            else if (window.Equals("weekly", StringComparison.OrdinalIgnoreCase))
            {
                weeklyRemaining = remaining;
                weeklyResetsAt = resetAt;
            }
        }

        if (sessionRemaining is null && weeklyRemaining is null)
            return AntigravityGroupSnapshot.Unavailable("No Antigravity quota");

        return AntigravityGroupSnapshot.FromUsage(
            sessionRemaining ?? weeklyRemaining ?? 0,
            weeklyRemaining ?? sessionRemaining ?? 0,
            sessionResetsAt,
            weeklyResetsAt);
    }

    internal static double ParseRemainingFraction(JsonElement bucket)
    {
        if (TryReadDoubleProperty(bucket, "remaining_fraction", "remainingFraction", out var fraction))
            return Math.Clamp(fraction * 100, 0, 100);

        if (bucket.TryGetProperty("reset_time", out _) || bucket.TryGetProperty("resetTime", out _))
            return 0;

        return 0;
    }

    internal static bool IsGeminiGroup(string displayName) =>
        displayName.Contains("gemini", StringComparison.OrdinalIgnoreCase);

    internal static bool IsThirdPartyGroup(string displayName) =>
        displayName.Contains("claude", StringComparison.OrdinalIgnoreCase)
        || displayName.Contains("gpt", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> ResolveAccessTokenAsync(CancellationToken cancellationToken)
    {
        var tokens = _tokenReader();
        if (tokens.IsAccessTokenValid())
            return tokens.AccessToken;

        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            return null;

        return await RefreshAccessTokenAsync(tokens.RefreshToken, cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = AntigravityOAuthAppCredentials.ClientId,
            ["client_secret"] = AntigravityOAuthAppCredentials.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token"
        });

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw AntigravityUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("access_token", out var tokenEl)
               && tokenEl.ValueKind == JsonValueKind.String
            ? tokenEl.GetString()
            : null;
    }

    private async Task<(string? ProjectId, string? PlanLabel)> LoadProjectInfoAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = CreateApiRequest(
            $"{CloudCodeBaseUrl}/v1internal:loadCodeAssist",
            accessToken,
            """{"metadata":{"ideType":"ANTIGRAVITY"}}""");

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw AntigravityUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var projectId = root.TryGetProperty("cloudaicompanionProject", out var projectEl)
                        && projectEl.ValueKind == JsonValueKind.String
            ? projectEl.GetString()
            : null;

        string? tier = null;
        if (root.TryGetProperty("paidTier", out var paidTier) && paidTier.ValueKind == JsonValueKind.Object
            && paidTier.TryGetProperty("id", out var paidTierId) && paidTierId.ValueKind == JsonValueKind.String)
            tier = paidTierId.GetString();
        else if (root.TryGetProperty("currentTier", out var currentTier) && currentTier.ValueKind == JsonValueKind.Object
                 && currentTier.TryGetProperty("id", out var currentTierId) && currentTierId.ValueKind == JsonValueKind.String)
            tier = currentTierId.GetString();

        return (projectId, FormatPlanLabel(tier));
    }

    private async Task<JsonElement> FetchQuotaSummaryAsync(
        string accessToken,
        string? projectId,
        CancellationToken cancellationToken)
    {
        var payload = string.IsNullOrWhiteSpace(projectId)
            ? "{}"
            : JsonSerializer.Serialize(new { project = projectId });

        using var request = CreateApiRequest(
            $"{CloudCodeBaseUrl}/v1internal:retrieveUserQuotaSummary",
            accessToken,
            payload);

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw AntigravityUsageException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static HttpRequestMessage CreateApiRequest(string url, string accessToken, string jsonBody)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("User-Agent", "Antigravity/1.0");
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        return request;
    }

    private static string? ReadStringProperty(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                return value.GetString();
        }

        return null;
    }

    private static bool TryReadDoubleProperty(JsonElement element, string snakeName, string camelName, out double value)
    {
        value = 0;
        if (element.TryGetProperty(snakeName, out var snakeEl) || element.TryGetProperty(camelName, out snakeEl))
        {
            if (snakeEl.ValueKind == JsonValueKind.Number && snakeEl.TryGetDouble(out value))
                return true;

            if (snakeEl.ValueKind == JsonValueKind.String
                && double.TryParse(snakeEl.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                return true;
        }

        return false;
    }

    private static DateTimeOffset? ParseResetTime(JsonElement bucket)
    {
        var text = ReadStringProperty(bucket, "reset_time", "resetTime");
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : null;
    }

    private static string? FormatPlanLabel(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier))
            return null;

        return tier switch
        {
            "free-tier" => "Free",
            "standard-tier" => "Pro",
            "ultra-tier" => "Ultra",
            _ when tier.Contains("pro", StringComparison.OrdinalIgnoreCase) => "Pro",
            _ when tier.Contains("ultra", StringComparison.OrdinalIgnoreCase) => "Ultra",
            _ => tier.Replace('-', ' ')
        };
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

internal sealed class AntigravityUsageException : Exception
{
    public AntigravityUsageException(string message) : base(message) { }

    public static AntigravityUsageException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        if (status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            return new AntigravityUsageException("Antigravity session expired — sign in again in Antigravity");

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("message", out var messageEl) && messageEl.ValueKind == JsonValueKind.String)
                    return new AntigravityUsageException(messageEl.GetString() ?? status.ToString());

                if (error.ValueKind == JsonValueKind.String)
                    return new AntigravityUsageException(error.GetString() ?? status.ToString());
            }
        }
        catch
        {
            // ignore
        }

        return new AntigravityUsageException($"HTTP {(int)status}");
    }
}

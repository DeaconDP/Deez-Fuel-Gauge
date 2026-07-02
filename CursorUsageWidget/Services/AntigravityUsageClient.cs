using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class AntigravityUsageClient : IDisposable
{
    private const string CloudCodeBaseUrl = "https://cloudcode-pa.googleapis.com";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;
    private readonly GeminiAuthResolver _authResolver;

    public AntigravityUsageClient(HttpClient? http = null, GeminiAuthResolver? authResolver = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
        _authResolver = authResolver ?? new GeminiAuthResolver();
    }

    public async Task<AntigravitySnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auth = _authResolver.Resolve();
            if (!auth.HasAuth)
            {
                settings.ProLastConnectionStatus = auth.FailureMessage
                    ?? "Sign in to Antigravity IDE or Gemini CLI on this machine";
                return AntigravitySnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            var accessToken = await ResolveAccessTokenAsync(auth, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                settings.ProLastConnectionStatus = "Gemini session expired — sign in again";
                return AntigravitySnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            var (projectId, planLabel) = await LoadProjectInfoAsync(accessToken, auth.Source, cancellationToken);
            var snapshot = await FetchQuotaAsync(accessToken, projectId, planLabel, cancellationToken);
            settings.ProLastConnectionStatus = snapshot.IsAvailable
                ? $"Connected ({snapshot.PlanLabel ?? "Gemini"})"
                : (snapshot.StatusMessage ?? "No Gemini quota");
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
            var auth = _authResolver.Resolve();
            if (!auth.HasAuth)
                return auth.FailureMessage ?? "Sign in to Antigravity IDE or Gemini CLI on this machine";

            var accessToken = await ResolveAccessTokenAsync(auth, cancellationToken);
            if (string.IsNullOrWhiteSpace(accessToken))
                return "Gemini session expired — sign in again";

            var (projectId, planLabel) = await LoadProjectInfoAsync(accessToken, auth.Source, cancellationToken);
            var snapshot = await FetchQuotaAsync(accessToken, projectId, planLabel, cancellationToken);
            return snapshot.IsAvailable
                ? $"Connected ({snapshot.PlanLabel ?? "Gemini"})"
                : (snapshot.StatusMessage ?? "No Gemini quota");
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

    private async Task<AntigravitySnapshot> FetchQuotaAsync(
        string accessToken,
        string? projectId,
        string? planLabel,
        CancellationToken cancellationToken)
    {
        var summary = await FetchQuotaSummaryAsync(accessToken, projectId, cancellationToken);
        var snapshot = ParseQuotaSummary(summary, planLabel);
        if (snapshot.IsAvailable)
            return snapshot;

        var userQuota = await TryFetchUserQuotaAsync(accessToken, projectId, cancellationToken);
        if (userQuota is null)
            return snapshot;

        return ParseUserQuota(userQuota.Value, planLabel);
    }

    internal static AntigravitySnapshot ParseQuotaSummary(JsonElement root, string? planLabel = null)
    {
        if (!root.TryGetProperty("quota_groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
            return AntigravitySnapshot.Unavailable("No Gemini quota");

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

        if (gemini is null && thirdParty is null)
            return AntigravitySnapshot.Unavailable("No Gemini quota");

        return AntigravitySnapshot.FromGroups(
            planLabel,
            gemini ?? AntigravityGroupSnapshot.Unavailable(),
            thirdParty ?? AntigravityGroupSnapshot.Unavailable());
    }

    internal static AntigravitySnapshot ParseUserQuota(JsonElement root, string? planLabel = null)
    {
        if (!root.TryGetProperty("buckets", out var buckets) || buckets.ValueKind != JsonValueKind.Array)
            return AntigravitySnapshot.Unavailable("No Gemini quota");

        var geminiGroup = ParsePerModelBuckets(buckets);
        if (!geminiGroup.IsAvailable)
            return AntigravitySnapshot.Unavailable("No Gemini quota");

        return AntigravitySnapshot.FromGroups(
            planLabel,
            geminiGroup,
            AntigravityGroupSnapshot.Unavailable());
    }

    internal static AntigravityGroupSnapshot ParsePerModelBuckets(JsonElement buckets)
    {
        double? sessionRemaining = null;
        double? weeklyRemaining = null;
        DateTimeOffset? sessionResetsAt = null;
        DateTimeOffset? weeklyResetsAt = null;
        var now = DateTimeOffset.UtcNow;

        foreach (var bucket in buckets.EnumerateArray())
        {
            var modelId = ReadStringProperty(bucket, "model_id", "modelId") ?? "";
            if (!IsGeminiModelId(modelId))
                continue;

            var remaining = ParseRemainingFraction(bucket);
            var resetAt = ParseResetTime(bucket);
            var isWeekly = resetAt is null || resetAt.Value - now > TimeSpan.FromHours(24);

            if (isWeekly)
            {
                if (weeklyRemaining is null || remaining < weeklyRemaining)
                {
                    weeklyRemaining = remaining;
                    weeklyResetsAt = resetAt;
                }
            }
            else if (sessionRemaining is null || remaining < sessionRemaining)
            {
                sessionRemaining = remaining;
                sessionResetsAt = resetAt;
            }
        }

        if (sessionRemaining is null && weeklyRemaining is null)
            return AntigravityGroupSnapshot.Unavailable("No Gemini quota");

        return AntigravityGroupSnapshot.FromUsage(
            sessionRemaining ?? weeklyRemaining ?? 0,
            weeklyRemaining ?? sessionRemaining ?? 0,
            sessionResetsAt,
            weeklyResetsAt);
    }

    internal static bool IsGeminiModelId(string modelId) =>
        modelId.Contains("gemini", StringComparison.OrdinalIgnoreCase);

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
            return AntigravityGroupSnapshot.Unavailable("No Gemini quota");

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

    private async Task<string?> ResolveAccessTokenAsync(GeminiAuthResult auth, CancellationToken cancellationToken)
    {
        var tokens = auth.Tokens;
        if (tokens.IsAccessTokenValid())
            return tokens.AccessToken;

        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            return null;

        if (string.IsNullOrWhiteSpace(auth.OAuthClientId) || string.IsNullOrWhiteSpace(auth.OAuthClientSecret))
            return null;

        return await RefreshAccessTokenAsync(
            tokens.RefreshToken,
            auth.OAuthClientId,
            auth.OAuthClientSecret,
            cancellationToken);
    }

    private async Task<string?> RefreshAccessTokenAsync(
        string refreshToken,
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
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
        GeminiAuthSource authSource,
        CancellationToken cancellationToken)
    {
        var payload = authSource == GeminiAuthSource.GeminiCli
            ? """{"metadata":{"ideType":"GEMINI_CLI","pluginType":"GEMINI"}}"""
            : """{"metadata":{"ideType":"ANTIGRAVITY"}}""";

        var userAgent = authSource == GeminiAuthSource.GeminiCli ? "GeminiCLI/1.0" : "Antigravity/1.0";
        using var request = CreateApiRequest(
            $"{CloudCodeBaseUrl}/v1internal:loadCodeAssist",
            accessToken,
            payload,
            userAgent);

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

    private async Task<JsonElement?> TryFetchUserQuotaAsync(
        string accessToken,
        string? projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = string.IsNullOrWhiteSpace(projectId)
                ? "{}"
                : JsonSerializer.Serialize(new { project = projectId });

            using var request = CreateApiRequest(
                $"{CloudCodeBaseUrl}/v1internal:retrieveUserQuota",
                accessToken,
                payload);

            using var response = await _http.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return null;

            using var document = JsonDocument.Parse(body);
            return document.RootElement.Clone();
        }
        catch
        {
            return null;
        }
    }

    private static HttpRequestMessage CreateApiRequest(
        string url,
        string accessToken,
        string jsonBody,
        string userAgent = "Antigravity/1.0")
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
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
            return new AntigravityUsageException("Gemini session expired — sign in again");

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

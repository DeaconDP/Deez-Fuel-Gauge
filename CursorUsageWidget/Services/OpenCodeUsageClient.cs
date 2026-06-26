using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class OpenCodeUsageClient : IDisposable
{
    private const string BaseUrl = "https://opencode.ai";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenCodeUsageClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<OpenCodeSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var session = CredentialStore.Retrieve(settings.ProSessionCredentialId);
        if (string.IsNullOrWhiteSpace(session))
            return OpenCodeSnapshot.Unavailable("Session cookie not set");

        var workspaceId = settings.WorkspaceId?.Trim();
        if (string.IsNullOrWhiteSpace(workspaceId))
            return OpenCodeSnapshot.Unavailable("Workspace ID not set");

        try
        {
            var official = await TryOfficialApiAsync(session, workspaceId, cancellationToken);
            if (official is not null)
            {
                settings.ProLastConnectionStatus = "Connected";
                return official;
            }

            var zenHtml = settings.ShowDirectSource
                ? await FetchPageAsync($"{BaseUrl}/workspace/{workspaceId}", session, cancellationToken)
                : null;

            var goHtml = settings.ShowProLimits
                ? await FetchPageAsync($"{BaseUrl}/workspace/{workspaceId}/go", session, cancellationToken)
                : null;

            var zen = zenHtml is not null ? ParseZenPage(zenHtml) : (null, null, null);
            var go = goHtml is not null ? ParseGoPage(goHtml) : (false, null, null, null);

            var snapshot = OpenCodeSnapshot.FromData(
                zen.balance,
                zen.monthlyCap,
                zen.monthlyUsed,
                go.rolling,
                go.weekly,
                go.monthly,
                go.hasSubscription);

            if (!snapshot.IsAvailable)
            {
                settings.ProLastConnectionStatus = "No usage data found — cookie may have expired";
                return OpenCodeSnapshot.Unavailable(settings.ProLastConnectionStatus);
            }

            settings.ProLastConnectionStatus = "Connected";
            return snapshot;
        }
        catch (OpenCodeUsageException ex)
        {
            settings.ProLastConnectionStatus = ex.Message;
            return OpenCodeSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.ProLastConnectionStatus = "Request failed";
            return OpenCodeSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(
        string sessionValue,
        string? workspaceId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionValue))
            return "Session cookie required";

        if (string.IsNullOrWhiteSpace(workspaceId))
            return "Workspace ID required";

        try
        {
            var html = await FetchPageAsync($"{BaseUrl}/workspace/{workspaceId.Trim()}", sessionValue, cancellationToken);
            if (html.Contains("sign in", StringComparison.OrdinalIgnoreCase) &&
                html.Contains("login", StringComparison.OrdinalIgnoreCase))
                return "Session expired — re-copy auth cookie from DevTools";

            return "Connected";
        }
        catch (OpenCodeUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static string BuildAuthCookieHeader(string sessionValue)
    {
        var trimmed = sessionValue.Trim();
        if (trimmed.StartsWith("auth=", StringComparison.OrdinalIgnoreCase))
            return trimmed;

        return $"auth={trimmed}";
    }

    internal static (decimal? balance, decimal? monthlyCap, decimal? monthlyUsed) ParseZenPage(string html)
    {
        decimal? balance = null;
        decimal? monthlyCap = null;
        decimal? monthlyUsed = null;

        var balanceMatch = Regex.Match(
            html,
            @"balance(?:Usd)?[""':\s]*[:=]\s*[""']?\$?([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase);
        if (balanceMatch.Success && decimal.TryParse(balanceMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var bal))
            balance = bal;

        var balanceAlt = Regex.Match(
            html,
            @"\$([0-9]+(?:\.[0-9]{2})?)\s*</[^>]+>\s*<[^>]+>\s*balance",
            RegexOptions.IgnoreCase);
        if (!balance.HasValue && balanceAlt.Success &&
            decimal.TryParse(balanceAlt.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var balAlt))
            balance = balAlt;

        var monthlyCapMatch = Regex.Match(
            html,
            @"monthly(?:Limit|Cap)(?:Usd)?[""':\s]*[:=]\s*[""']?\$?([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase);
        if (monthlyCapMatch.Success &&
            decimal.TryParse(monthlyCapMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var cap))
            monthlyCap = cap;

        var monthlyUsedMatch = Regex.Match(
            html,
            @"monthlyUsage[""':\s]*[:=]\s*\{[^}]*usage(?:Dollars|Usd)?[""':\s]*[:=]\s*([0-9]+(?:\.[0-9]+)?)",
            RegexOptions.IgnoreCase);
        if (monthlyUsedMatch.Success &&
            decimal.TryParse(monthlyUsedMatch.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var used))
            monthlyUsed = used;

        return (balance, monthlyCap, monthlyUsed);
    }

    internal static (bool hasSubscription, OpenCodeWindowSnapshot? rolling, OpenCodeWindowSnapshot? weekly, OpenCodeWindowSnapshot? monthly) ParseGoPage(string html)
    {
        var rolling = ParseUsageWindow(html, "rollingUsage");
        var weekly = ParseUsageWindow(html, "weeklyUsage");
        var monthly = ParseUsageWindow(html, "monthlyUsage");
        var hasSubscription = rolling is not null || weekly is not null || monthly is not null ||
                              html.Contains("Go plan", StringComparison.OrdinalIgnoreCase) ||
                              html.Contains("OpenCode Go", StringComparison.OrdinalIgnoreCase);

        return (hasSubscription, rolling, weekly, monthly);
    }

    internal static OpenCodeWindowSnapshot? ParseUsageWindow(string html, string fieldName)
    {
        var pattern = $@"{fieldName}:\$R\[\d+\]=\{{[^}}]*usagePercent:(\d+(?:\.\d+)?)[^}}]*resetInSec:(\d+)[^}}]*\}}";
        var match = Regex.Match(html, pattern);
        if (!match.Success)
        {
            pattern = $@"{fieldName}:\$R\[\d+\]=\{{[^}}]*resetInSec:(\d+)[^}}]*usagePercent:(\d+(?:\.\d+)?)[^}}]*\}}";
            match = Regex.Match(html, pattern);
            if (!match.Success)
                return null;

            if (!double.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percentAlt) ||
                !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetSecAlt))
                return null;

            return OpenCodeWindowSnapshot.FromUsage(
                percentAlt,
                DateTimeOffset.UtcNow.AddSeconds(resetSecAlt));
        }

        if (!double.TryParse(match.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var percent) ||
            !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var resetSec))
            return null;

        return OpenCodeWindowSnapshot.FromUsage(
            percent,
            DateTimeOffset.UtcNow.AddSeconds(resetSec));
    }

    private async Task<OpenCodeSnapshot?> TryOfficialApiAsync(
        string session,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        var apiKey = await TryReadLocalApiKeyAsync();
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;

        using var zenRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/zen/v1/balance");
        zenRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var zenResponse = await _http.SendAsync(zenRequest, cancellationToken);
        if (!zenResponse.IsSuccessStatusCode)
            return null;

        await using var zenStream = await zenResponse.Content.ReadAsStreamAsync(cancellationToken);
        using var zenDoc = await JsonDocument.ParseAsync(zenStream, cancellationToken: cancellationToken);

        decimal? balance = null;
        if (zenDoc.RootElement.TryGetProperty("balance", out var balanceEl) && balanceEl.ValueKind == JsonValueKind.Number)
            balance = balanceEl.GetDecimal();

        using var goRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/zen/go/v1/usage");
        goRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var goResponse = await _http.SendAsync(goRequest, cancellationToken);
        OpenCodeWindowSnapshot? rolling = null;
        OpenCodeWindowSnapshot? weekly = null;
        OpenCodeWindowSnapshot? monthly = null;
        var hasGo = false;

        if (goResponse.IsSuccessStatusCode)
        {
            await using var goStream = await goResponse.Content.ReadAsStreamAsync(cancellationToken);
            using var goDoc = await JsonDocument.ParseAsync(goStream, cancellationToken: cancellationToken);
            rolling = ParseOfficialWindow(goDoc.RootElement, "rolling5h", "rolling");
            weekly = ParseOfficialWindow(goDoc.RootElement, "weekly");
            monthly = ParseOfficialWindow(goDoc.RootElement, "monthly");
            hasGo = rolling is not null || weekly is not null || monthly is not null;
        }

        if (balance is null && !hasGo)
            return null;

        return OpenCodeSnapshot.FromData(balance, null, null, rolling, weekly, monthly, hasGo);
    }

    private static OpenCodeWindowSnapshot? ParseOfficialWindow(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var window))
                continue;

            var percent = window.TryGetProperty("usagePercent", out var percentEl) && percentEl.ValueKind == JsonValueKind.Number
                ? percentEl.GetDouble()
                : window.TryGetProperty("percent", out var percentAlt) && percentAlt.ValueKind == JsonValueKind.Number
                    ? percentAlt.GetDouble()
                    : (double?)null;

            var resetSec = window.TryGetProperty("resetInSec", out var resetEl) && resetEl.ValueKind == JsonValueKind.Number
                ? resetEl.GetInt32()
                : (int?)null;

            if (percent is null)
                continue;

            return OpenCodeWindowSnapshot.FromUsage(
                percent.Value,
                resetSec is { } sec ? DateTimeOffset.UtcNow.AddSeconds(sec) : null);
        }

        return null;
    }

    private static async Task<string?> TryReadLocalApiKeyAsync()
    {
        var path = ResolveAuthFilePath();
        if (path is null || !File.Exists(path))
            return null;

        try
        {
            await using var stream = File.OpenRead(path);
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("opencode", out var opencode) &&
                opencode.TryGetProperty("apiKey", out var apiKey) &&
                apiKey.ValueKind == JsonValueKind.String)
                return apiKey.GetString();

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object)
                    continue;

                if (property.Value.TryGetProperty("apiKey", out var key) && key.ValueKind == JsonValueKind.String)
                    return key.GetString();
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    internal static string? ResolveAuthFilePath()
    {
        var overridePath = Environment.GetEnvironmentVariable("OPENCODE_AUTH_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            return Path.IsPathRooted(overridePath)
                ? overridePath
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "opencode", overridePath);
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "opencode",
            "auth.json");
    }

    private async Task<string> FetchPageAsync(string url, string session, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("Cookie", BuildAuthCookieHeader(session));
        request.Headers.TryAddWithoutValidation("User-Agent", "CursorUsageWidget/1.0");
        request.Headers.TryAddWithoutValidation("Accept", "text/html");

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
            throw new OpenCodeUsageException("Session expired — re-copy auth cookie from DevTools");

        if (!response.IsSuccessStatusCode)
            throw new OpenCodeUsageException($"Dashboard request failed ({(int)response.StatusCode})");

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class OpenCodeUsageException : Exception
{
    public OpenCodeUsageException(string message) : base(message) { }
}

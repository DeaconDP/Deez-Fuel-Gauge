using System.Net.Http.Headers;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class OpenRouterUsageClient : IDisposable
{
    private const string BaseUrl = "https://openrouter.ai/api/v1";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenRouterUsageClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<OpenRouterSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = CredentialStore.Retrieve(settings.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return OpenRouterSnapshot.Unavailable("API key not set");

        try
        {
            var keyData = await FetchKeyAsync(apiKey, cancellationToken);
            var credits = await TryFetchCreditsAsync(apiKey, cancellationToken);
            var snapshot = MergeResponses(keyData, credits);
            settings.LastConnectionStatus = snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
            return snapshot;
        }
        catch (OpenRouterUsageException ex)
        {
            settings.LastConnectionStatus = ex.Message;
            return OpenRouterSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.LastConnectionStatus = "Request failed";
            return OpenRouterSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "API key required";

        try
        {
            var snapshot = MergeResponses(
                await FetchKeyAsync(apiKey, cancellationToken),
                await TryFetchCreditsAsync(apiKey, cancellationToken));
            return snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
        }
        catch (OpenRouterUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static OpenRouterSnapshot MergeResponses(KeyResponseData key, CreditsResponseData? credits)
    {
        double? balance = credits?.BalanceUsd;
        return OpenRouterSnapshot.FromResponses(
            balance,
            key.LimitUsd,
            key.LimitRemainingUsd,
            key.DailySpendUsd,
            key.WeeklySpendUsd,
            key.MonthlySpendUsd);
    }

    internal static KeyResponseData ParseKeyResponse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data))
            throw new OpenRouterUsageException("Invalid key response");

        var limit = ReadNullableDouble(data, "limit");
        var limitRemaining = ReadNullableDouble(data, "limit_remaining");
        var daily = ReadDouble(data, "usage_daily");
        var weekly = ReadDouble(data, "usage_weekly");
        var monthly = ReadDouble(data, "usage_monthly");

        return new KeyResponseData(limit, limitRemaining, daily, weekly, monthly);
    }

    internal static CreditsResponseData? ParseCreditsResponse(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data))
            return null;

        var totalCredits = ReadDouble(data, "total_credits");
        var totalUsage = ReadDouble(data, "total_usage");
        return new CreditsResponseData(totalCredits - totalUsage);
    }

    private async Task<KeyResponseData> FetchKeyAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/key");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new OpenRouterUsageException("Invalid API key");

        if (!response.IsSuccessStatusCode)
            throw new OpenRouterUsageException($"Key request failed ({(int)response.StatusCode})");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseKeyResponse(doc.RootElement);
    }

    private async Task<CreditsResponseData?> TryFetchCreditsAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/credits");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.Unauthorized)
            return null;

        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseCreditsResponse(doc.RootElement);
    }

    private static double ReadDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : 0;

    private static double? ReadNullableDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind == JsonValueKind.Null)
            return null;

        return value.ValueKind == JsonValueKind.Number ? value.GetDouble() : null;
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }

    internal readonly record struct KeyResponseData(
        double? LimitUsd,
        double? LimitRemainingUsd,
        double DailySpendUsd,
        double WeeklySpendUsd,
        double MonthlySpendUsd);

    internal readonly record struct CreditsResponseData(double BalanceUsd);
}

public sealed class OpenRouterUsageException : Exception
{
    public OpenRouterUsageException(string message) : base(message) { }
}

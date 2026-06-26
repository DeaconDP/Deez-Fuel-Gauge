using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public sealed class AnthropicBillingClient : IDisposable
{
    private const string ApiVersion = "2023-06-01";
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public AnthropicBillingClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<DirectProviderSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        long? billingCycleStartMs,
        long? billingCycleEndMs,
        CancellationToken cancellationToken = default)
    {
        var apiKey = CredentialStore.Retrieve(settings.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return DirectProviderSnapshot.Unavailable("Admin API key not set");

        if (!apiKey.StartsWith("sk-ant-admin", StringComparison.OrdinalIgnoreCase))
        {
            settings.LastConnectionStatus = "Admin API key required (sk-ant-admin...)";
            return DirectProviderSnapshot.Unavailable(settings.LastConnectionStatus);
        }

        var budget = settings.MonthlyBudgetUsd is > 0
            ? (double)settings.MonthlyBudgetUsd.Value
            : (double?)null;

        var (startingAt, endingAt) = ResolvePeriodIso(billingCycleStartMs, billingCycleEndMs);

        try
        {
            var spendUsd = await FetchCostsAsync(apiKey, startingAt, endingAt, cancellationToken);
            var (inputTokens, outputTokens) = await FetchUsageAsync(apiKey, startingAt, endingAt, cancellationToken);

            settings.LastConnectionStatus = "Connected";
            return DirectProviderSnapshot.FromBilling(
                spendUsd,
                budget,
                inputTokens,
                outputTokens,
                "Priority Tier costs may not appear in cost reports");
        }
        catch (AnthropicBillingException ex)
        {
            settings.LastConnectionStatus = ex.Message;
            return DirectProviderSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.LastConnectionStatus = "Request failed";
            return DirectProviderSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "API key required";

        if (!apiKey.StartsWith("sk-ant-admin", StringComparison.OrdinalIgnoreCase))
            return "Admin API key required (sk-ant-admin...)";

        var (startingAt, endingAt) = BillingPeriodHelper.CurrentCalendarMonthIso8601();
        try
        {
            await FetchCostsAsync(apiKey, startingAt, endingAt, cancellationToken);
            return "Connected";
        }
        catch (AnthropicBillingException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static double ParseCostResponse(JsonElement root)
    {
        var totalCents = 0.0;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return 0;

        foreach (var bucket in data.EnumerateArray())
        {
            if (!bucket.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var result in results.EnumerateArray())
            {
                if (result.TryGetProperty("amount", out var amountEl))
                {
                    if (amountEl.ValueKind == JsonValueKind.String
                        && double.TryParse(amountEl.GetString(), System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out var cents))
                        totalCents += cents;
                    else if (amountEl.ValueKind == JsonValueKind.Number)
                        totalCents += amountEl.GetDouble();
                }
                else if (result.TryGetProperty("cost_usd", out var usdEl) && usdEl.ValueKind == JsonValueKind.Number)
                {
                    totalCents += usdEl.GetDouble() * 100;
                }
            }
        }

        return totalCents / 100.0;
    }

    internal static (long InputTokens, long OutputTokens) ParseUsageResponse(JsonElement root)
    {
        long input = 0;
        long output = 0;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return (input, output);

        foreach (var bucket in data.EnumerateArray())
        {
            if (!bucket.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var result in results.EnumerateArray())
            {
                if (result.TryGetProperty("input_tokens", out var inEl))
                    input += inEl.GetInt64();
                if (result.TryGetProperty("output_tokens", out var outEl))
                    output += outEl.GetInt64();
                if (result.TryGetProperty("cache_read_input_tokens", out var cacheEl))
                    input += cacheEl.GetInt64();
            }
        }

        return (input, output);
    }

    private async Task<double> FetchCostsAsync(
        string apiKey,
        string startingAt,
        string endingAt,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.anthropic.com/v1/organizations/cost_report?starting_at={Uri.EscapeDataString(startingAt)}&ending_at={Uri.EscapeDataString(endingAt)}&bucket_width=1d&limit=31";
        using var request = CreateRequest(url, apiKey);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw AnthropicBillingException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseCostResponse(document.RootElement);
    }

    private async Task<(long InputTokens, long OutputTokens)> FetchUsageAsync(
        string apiKey,
        string startingAt,
        string endingAt,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.anthropic.com/v1/organizations/usage_report/messages?starting_at={Uri.EscapeDataString(startingAt)}&ending_at={Uri.EscapeDataString(endingAt)}&bucket_width=1d&limit=31";
        using var request = CreateRequest(url, apiKey);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (0, 0);

        using var document = JsonDocument.Parse(body);
        return ParseUsageResponse(document.RootElement);
    }

    private static HttpRequestMessage CreateRequest(string url, string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", ApiVersion);
        return request;
    }

    private static (string StartingAt, string EndingAt) ResolvePeriodIso(long? startMs, long? endMs)
    {
        var cursor = BillingPeriodHelper.FromCursorCycle(startMs, endMs);
        if (cursor is not null)
        {
            var start = DateTimeOffset.FromUnixTimeMilliseconds(cursor.Value.StartMs).UtcDateTime;
            var end = DateTimeOffset.FromUnixTimeMilliseconds(cursor.Value.EndMs).UtcDateTime;
            return (start.ToString("yyyy-MM-ddTHH:mm:ssZ"), end.ToString("yyyy-MM-ddTHH:mm:ssZ"));
        }

        return BillingPeriodHelper.CurrentCalendarMonthIso8601();
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

internal sealed class AnthropicBillingException : Exception
{
    public AnthropicBillingException(string message) : base(message) { }

    public static AnthropicBillingException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error)
                && error.TryGetProperty("message", out var messageEl))
                return new AnthropicBillingException(messageEl.GetString() ?? status.ToString());
        }
        catch
        {
            // ignore
        }

        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized => new AnthropicBillingException("Invalid API key"),
            System.Net.HttpStatusCode.Forbidden => new AnthropicBillingException("Admin API key required"),
            _ => new AnthropicBillingException($"HTTP {(int)status}")
        };
    }
}

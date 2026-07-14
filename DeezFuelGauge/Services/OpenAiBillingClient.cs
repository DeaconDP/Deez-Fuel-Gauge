using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class OpenAiBillingClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public OpenAiBillingClient(HttpClient? http = null)
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
            return DirectProviderSnapshot.Unavailable("API key not set");

        var budget = settings.MonthlyBudgetUsd is > 0
            ? (double)settings.MonthlyBudgetUsd.Value
            : (double?)null;

        var (startUnix, endUnix) = ResolvePeriod(billingCycleStartMs, billingCycleEndMs);

        OpenAiBillingException? grantsError = null;
        try
        {
            var grants = await FetchCreditGrantsAsync(apiKey, settings.OrganizationId, cancellationToken);
            if (grants is { } creditGrants)
            {
                settings.LastConnectionStatus = "Connected";
                return DirectProviderSnapshot.FromCreditGrants(
                    creditGrants.GrantedUsd,
                    creditGrants.UsedUsd,
                    creditGrants.RemainingUsd);
            }
        }
        catch (OpenAiBillingException ex)
        {
            grantsError = ex;
        }

        try
        {
            var spendUsd = await FetchCostsAsync(apiKey, settings.OrganizationId, startUnix, endUnix, cancellationToken);
            var (inputTokens, outputTokens) = await FetchUsageAsync(
                apiKey, settings.OrganizationId, startUnix, endUnix, cancellationToken);

            settings.LastConnectionStatus = "Connected";
            return DirectProviderSnapshot.FromBilling(spendUsd, budget, inputTokens, outputTokens);
        }
        catch (OpenAiBillingException ex)
        {
            var message = ResolveFailureMessage(grantsError, ex);
            settings.LastConnectionStatus = message;
            return DirectProviderSnapshot.Unavailable(message);
        }
        catch (Exception)
        {
            var message = ResolveFailureMessage(grantsError, null);
            settings.LastConnectionStatus = message;
            return DirectProviderSnapshot.Unavailable(message);
        }
    }

    public async Task<string> TestConnectionAsync(
        string apiKey,
        string? organizationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "API key required";

        try
        {
            var grants = await FetchCreditGrantsAsync(apiKey, organizationId, cancellationToken);
            if (grants is not null)
                return "Connected";
        }
        catch (OpenAiBillingException)
        {
            // Fall through to Admin costs.
        }

        var (startUnix, _) = BillingPeriodHelper.CurrentCalendarMonthUtc();
        try
        {
            await FetchCostsAsync(apiKey, organizationId, startUnix, startUnix + 86400, cancellationToken);
            return "Connected";
        }
        catch (OpenAiBillingException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static double ParseCostsResponse(JsonElement root)
    {
        var total = 0.0;
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return total;

        foreach (var bucket in data.EnumerateArray())
        {
            if (!bucket.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var result in results.EnumerateArray())
            {
                if (result.TryGetProperty("amount", out var amountEl))
                    total += ParseUsdAmount(amountEl);
                else if (result.TryGetProperty("cost", out var costEl) && costEl.ValueKind == JsonValueKind.Number)
                    total += costEl.GetDouble();
            }
        }

        return total;
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
            }
        }

        return (input, output);
    }

    internal static (double GrantedUsd, double UsedUsd, double RemainingUsd)? ParseCreditGrantsResponse(JsonElement root)
    {
        var hasGranted = TryReadUsd(root, "total_granted", out var granted);
        var hasUsed = TryReadUsd(root, "total_used", out var used);
        var hasRemaining = TryReadUsd(root, "total_available", out var remaining)
                           || TryReadUsd(root, "total_paid_available", out remaining);

        if (!hasGranted && !hasUsed && !hasRemaining)
            return null;

        if (!hasGranted)
            granted = 0;
        if (!hasUsed)
            used = hasGranted && hasRemaining ? Math.Max(0, granted - remaining) : 0;
        if (!hasRemaining)
            remaining = hasGranted ? Math.Max(0, granted - used) : 0;

        return (granted, used, remaining);
    }

    private async Task<(double GrantedUsd, double UsedUsd, double RemainingUsd)?> FetchCreditGrantsAsync(
        string apiKey,
        string? organizationId,
        CancellationToken cancellationToken)
    {
        const string url = "https://api.openai.com/v1/dashboard/billing/credit_grants";
        using var request = CreateRequest(HttpMethod.Get, url, apiKey, organizationId);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw OpenAiBillingException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseCreditGrantsResponse(document.RootElement);
    }

    private async Task<double> FetchCostsAsync(
        string apiKey,
        string? organizationId,
        long startUnix,
        long endUnix,
        CancellationToken cancellationToken)
    {
        var url = $"https://api.openai.com/v1/organization/costs?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31";
        using var request = CreateRequest(HttpMethod.Get, url, apiKey, organizationId);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw OpenAiBillingException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseCostsResponse(document.RootElement);
    }

    private async Task<(long InputTokens, long OutputTokens)> FetchUsageAsync(
        string apiKey,
        string? organizationId,
        long startUnix,
        long endUnix,
        CancellationToken cancellationToken)
    {
        var url =
            $"https://api.openai.com/v1/organization/usage/completions?start_time={startUnix}&end_time={endUnix}&bucket_width=1d&limit=31";
        using var request = CreateRequest(HttpMethod.Get, url, apiKey, organizationId);
        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return (0, 0);

        using var document = JsonDocument.Parse(body);
        return ParseUsageResponse(document.RootElement);
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, string apiKey, string? organizationId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        if (!string.IsNullOrWhiteSpace(organizationId))
            request.Headers.TryAddWithoutValidation("OpenAI-Organization", organizationId);
        return request;
    }

    private static (long StartUnix, long EndUnix) ResolvePeriod(long? startMs, long? endMs)
    {
        var cursor = BillingPeriodHelper.FromCursorCycle(startMs, endMs);
        if (cursor is not null)
            return (cursor.Value.StartMs / 1000, cursor.Value.EndMs / 1000);

        return BillingPeriodHelper.CurrentCalendarMonthUtc();
    }

    private static double ParseUsdAmount(JsonElement amountEl)
    {
        if (amountEl.ValueKind == JsonValueKind.Number)
            return amountEl.GetDouble();

        if (amountEl.ValueKind == JsonValueKind.Object)
        {
            if (amountEl.TryGetProperty("value", out var valueEl))
            {
                if (valueEl.ValueKind == JsonValueKind.Number)
                    return valueEl.GetDouble();
                if (valueEl.ValueKind == JsonValueKind.String
                    && double.TryParse(valueEl.GetString(), System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
            }
        }

        return 0;
    }

    private static bool TryReadUsd(JsonElement root, string propertyName, out double value)
    {
        value = 0;
        if (!root.TryGetProperty(propertyName, out var el))
            return false;

        if (el.ValueKind == JsonValueKind.Number)
        {
            value = el.GetDouble();
            return true;
        }

        if (el.ValueKind == JsonValueKind.String
            && double.TryParse(el.GetString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static string ResolveFailureMessage(OpenAiBillingException? grantsError, OpenAiBillingException? costsError)
    {
        if (costsError is not null && !IsCreditGrantsSessionOnly(costsError.Message))
            return costsError.Message;

        if (grantsError is not null && !IsCreditGrantsSessionOnly(grantsError.Message))
            return grantsError.Message;

        return costsError?.Message
               ?? grantsError?.Message
               ?? "Unable to read OpenAI API billing";
    }

    private static bool IsCreditGrantsSessionOnly(string message) =>
        message.Contains("session key", StringComparison.OrdinalIgnoreCase)
        || message.Contains("credit balance unavailable", StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

internal sealed class OpenAiBillingException : Exception
{
    public OpenAiBillingException(string message) : base(message) { }

    public static OpenAiBillingException FromResponse(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                string? msg = null;
                if (error.ValueKind == JsonValueKind.Object
                    && error.TryGetProperty("message", out var messageEl))
                    msg = messageEl.GetString();
                else if (error.ValueKind == JsonValueKind.String)
                    msg = error.GetString();

                if (!string.IsNullOrWhiteSpace(msg))
                {
                    if (msg.Contains("session key", StringComparison.OrdinalIgnoreCase)
                        && msg.Contains("credit_grants", StringComparison.OrdinalIgnoreCase))
                        return new OpenAiBillingException("Credit balance unavailable for this key type");

                    if (msg.Contains("api.usage.read", StringComparison.OrdinalIgnoreCase)
                        || msg.Contains("insufficient permissions", StringComparison.OrdinalIgnoreCase))
                        return new OpenAiBillingException("Admin key with api.usage.read required");

                    if (msg.Contains("project", StringComparison.OrdinalIgnoreCase)
                        && (msg.Contains("billing", StringComparison.OrdinalIgnoreCase)
                            || msg.Contains("organization", StringComparison.OrdinalIgnoreCase)))
                        return new OpenAiBillingException("Project keys cannot read org billing — use a user or admin key");

                    return new OpenAiBillingException(msg);
                }
            }
        }
        catch
        {
            // ignore parse errors
        }

        return status switch
        {
            System.Net.HttpStatusCode.Unauthorized => new OpenAiBillingException("Invalid API key"),
            System.Net.HttpStatusCode.Forbidden => new OpenAiBillingException("Admin key with api.usage.read required"),
            System.Net.HttpStatusCode.NotFound => new OpenAiBillingException("Not found — check organization ID"),
            _ => new OpenAiBillingException($"HTTP {(int)status}")
        };
    }
}

using System.Net.Http.Headers;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed class FalUsageClient : IDisposable
{
    private const string BillingUrl = "https://api.fal.ai/v1/account/billing?expand=credits";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public FalUsageClient(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public async Task<FalSnapshot> FetchAsync(
        ProviderBillingSettings settings,
        CancellationToken cancellationToken = default)
    {
        var apiKey = CredentialStore.Retrieve(settings.CredentialId);
        if (string.IsNullOrWhiteSpace(apiKey))
            return FalSnapshot.Unavailable("Admin API key not set");

        try
        {
            var snapshot = await FetchBillingAsync(apiKey, cancellationToken);
            settings.LastConnectionStatus = snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
            return snapshot;
        }
        catch (FalUsageException ex)
        {
            settings.LastConnectionStatus = ex.Message;
            return FalSnapshot.Unavailable(ex.Message);
        }
        catch (Exception)
        {
            settings.LastConnectionStatus = "Request failed";
            return FalSnapshot.Unavailable("Request failed");
        }
    }

    public async Task<string> TestConnectionAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return "Admin API key required";

        try
        {
            var snapshot = await FetchBillingAsync(apiKey, cancellationToken);
            return snapshot.IsAvailable ? "Connected" : (snapshot.StatusMessage ?? "Unavailable");
        }
        catch (FalUsageException ex)
        {
            return ex.Message;
        }
        catch (Exception)
        {
            return "Request failed";
        }
    }

    internal static FalSnapshot ParseBillingResponse(JsonElement root)
    {
        if (!root.TryGetProperty("credits", out var credits) || credits.ValueKind != JsonValueKind.Object)
            throw new FalUsageException("Billing response missing credits (use Admin API key)");

        if (!credits.TryGetProperty("current_balance", out var balanceEl) || balanceEl.ValueKind != JsonValueKind.Number)
            throw new FalUsageException("Invalid credits balance");

        var balance = balanceEl.GetDouble();
        var currency = credits.TryGetProperty("currency", out var currencyEl) && currencyEl.ValueKind == JsonValueKind.String
            ? currencyEl.GetString()
            : "USD";

        return FalSnapshot.FromBalance(balance, currency);
    }

    private async Task<FalSnapshot> FetchBillingAsync(string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BillingUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Key", apiKey.Trim());

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            throw new FalUsageException("Invalid or non-Admin API key");

        if (!response.IsSuccessStatusCode)
            throw new FalUsageException($"Billing request failed ({(int)response.StatusCode})");

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseBillingResponse(doc.RootElement);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class FalUsageException : Exception
{
    public FalUsageException(string message) : base(message) { }
}

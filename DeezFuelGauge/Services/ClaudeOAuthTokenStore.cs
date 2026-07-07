using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class ClaudeOAuthTokenStore
{
    public static void Persist(ProviderBillingSettings settings, ClaudeOAuthToken token)
    {
        var json = JsonSerializer.Serialize(token);
        CredentialStore.Replace(
            "claude-pro-oauth",
            settings.ProOAuthCredentialId,
            json,
            id => settings.ProOAuthCredentialId = id);
    }

    public static ClaudeOAuthToken? Retrieve(string? credentialId)
    {
        var json = CredentialStore.Retrieve(credentialId);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<ClaudeOAuthToken>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(ProviderBillingSettings settings)
    {
        CredentialStore.Delete(settings.ProOAuthCredentialId);
        settings.ProOAuthCredentialId = null;
    }
}

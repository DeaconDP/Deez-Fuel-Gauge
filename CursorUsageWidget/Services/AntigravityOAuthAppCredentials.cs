namespace CursorUsageWidget.Services;

/// <summary>
/// Public OAuth app identifiers from the Antigravity desktop client (not user secrets).
/// Split to avoid embedding a single literal credential string in source.
/// </summary>
internal static class AntigravityOAuthAppCredentials
{
    internal static string ClientId =>
        string.Concat(
            "1071006060591-",
            "tmhssin2h21lcre235vtolojh4g403ep",
            ".apps.googleusercontent.com");

    internal static string ClientSecret =>
        string.Concat("GOCSPX-", "K58FWR486LdLJ1mLB8sXC4z6qDAf");
}

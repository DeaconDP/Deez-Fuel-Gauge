namespace CursorUsageWidget.Services;

public sealed class AntigravityOAuthTokens
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }

    public bool IsAccessTokenValid(DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(AccessToken))
            return false;

        var observedAt = now ?? DateTimeOffset.UtcNow;
        return ExpiresAt is null || ExpiresAt > observedAt.AddMinutes(1);
    }
}

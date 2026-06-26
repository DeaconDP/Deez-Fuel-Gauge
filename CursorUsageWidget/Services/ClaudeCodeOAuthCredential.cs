namespace CursorUsageWidget.Services;

public sealed class ClaudeCodeOAuthCredential
{
    public string AccessToken { get; init; } = "";
    public long? ExpiresAt { get; init; }

    public bool IsExpired =>
        ExpiresAt is { } expiresAt && DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= expiresAt;
}

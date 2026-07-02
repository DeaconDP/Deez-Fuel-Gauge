namespace DeezFuelGauge.Models;

public sealed class ClaudeOAuthToken
{
    public string AccessToken { get; init; } = "";
    public string? RefreshToken { get; init; }
    public long ExpiresAtUnixMs { get; init; }

    public bool IsExpired =>
        DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() >= ExpiresAtUnixMs;
}

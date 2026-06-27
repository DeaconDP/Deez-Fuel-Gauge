namespace CursorUsageWidget.Models;

public sealed class RefreshResult
{
    public UsageSnapshot Snapshot { get; init; } = UsageSnapshot.Error("Not refreshed");
    public DateTimeOffset RefreshedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool CursorFetchSucceeded { get; init; }
    public string? CursorError { get; init; }
    public IReadOnlyDictionary<string, ProviderRefreshStatus> ProviderStatuses { get; init; }
        = new Dictionary<string, ProviderRefreshStatus>();

    public static RefreshResult FromSnapshot(UsageSnapshot snapshot, DateTimeOffset refreshedAt) =>
        new()
        {
            Snapshot = snapshot,
            RefreshedAt = refreshedAt,
            CursorFetchSucceeded = !snapshot.IsError
        };
}

public sealed class ProviderRefreshStatus
{
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsDegraded { get; init; }

    public static ProviderRefreshStatus Ok() => new() { Succeeded = true };

    public static ProviderRefreshStatus Failed(string message, bool degraded = false) =>
        new() { Succeeded = false, ErrorMessage = message, IsDegraded = degraded };
}

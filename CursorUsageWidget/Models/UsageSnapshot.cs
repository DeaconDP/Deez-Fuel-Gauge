namespace CursorUsageWidget.Models;

public sealed class UsageSnapshot
{
    public double PercentUsed { get; init; }
    public string RemainingLabel { get; init; } = "";
    public bool IsError { get; init; }
    public string? ErrorMessage { get; init; }

    public static UsageSnapshot Error(string message) => new()
    {
        IsError = true,
        ErrorMessage = message,
        PercentUsed = 0,
        RemainingLabel = message
    };
}

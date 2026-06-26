namespace CursorUsageWidget.Models;

public sealed record EasySetupResult(
    string? StatusMessage,
    string? Summary = null,
    bool LaunchedCodexLogin = false,
    bool OpenedExternalUrl = false);

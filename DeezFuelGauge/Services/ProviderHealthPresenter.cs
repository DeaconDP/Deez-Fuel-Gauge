namespace DeezFuelGauge.Services;

public static class ProviderHealthPresenter
{
    private static readonly Dictionary<string, string> ProviderLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["openai-platform"] = "OpenAI Platform",
        ["codex"] = "Codex",
        ["antigravity"] = "Antigravity",
        ["openrouter"] = "OpenRouter",
        ["opencode"] = "OpenCode"
    };

    public static string FormatDegradedMessage(string providerKey, string? detail) =>
        ProviderLabels.TryGetValue(providerKey, out var label)
            ? $"{label} unavailable — API may have changed"
            : detail ?? "Provider unavailable";

    public static string FormatHeadlineBadge(string? degradedMessage) =>
        string.IsNullOrWhiteSpace(degradedMessage) ? "" : " ⚠";
}

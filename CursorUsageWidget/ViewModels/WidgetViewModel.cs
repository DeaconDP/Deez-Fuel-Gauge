namespace CursorUsageWidget.ViewModels;

using CursorUsageWidget.Services;

public sealed class ProviderSectionViewModel
{
    public string Title { get; init; } = "";
    public double HeadlinePercent { get; set; }
    public bool IsExpanded { get; set; }
    public string? DegradedMessage { get; set; }
    public bool HasDegradedState => !string.IsNullOrWhiteSpace(DegradedMessage);
}

public sealed class WidgetViewModel
{
    public ProviderSectionViewModel Cursor { get; } = new() { Title = "Cursor" };
    public ProviderSectionViewModel OpenAi { get; } = new() { Title = "OpenAI" };
    public ProviderSectionViewModel Claude { get; } = new() { Title = "Claude" };
    public ProviderSectionViewModel Gemini { get; } = new() { Title = "Gemini" };
    public ProviderSectionViewModel OpenRouter { get; } = new() { Title = "OpenRouter" };
    public ProviderSectionViewModel OpenCode { get; } = new() { Title = "OpenCode" };

    public DateTimeOffset? LastRefreshedAt { get; set; }

    public string LastRefreshedLabel =>
        LastRefreshedAt is { } at
            ? $"Updated {at.ToLocalTime():HH:mm}"
            : "";

    public void ApplyRefreshResult(Models.RefreshResult result)
    {
        LastRefreshedAt = result.RefreshedAt;
        ClearDegradedStates();

        foreach (var (key, status) in result.ProviderStatuses)
        {
            if (status.Succeeded || !status.IsDegraded)
                continue;

            var message = ProviderHealthPresenter.FormatDegradedMessage(key, status.ErrorMessage);
            AssignDegradedMessage(key, message);
        }
    }

    private void ClearDegradedStates()
    {
        OpenAi.DegradedMessage = null;
        Claude.DegradedMessage = null;
        Gemini.DegradedMessage = null;
        OpenRouter.DegradedMessage = null;
        OpenCode.DegradedMessage = null;
    }

    private void AssignDegradedMessage(string providerKey, string message)
    {
        switch (providerKey)
        {
            case "openai-platform":
            case "codex":
                OpenAi.DegradedMessage ??= message;
                break;
            case "claude-pro":
            case "claude-api":
                Claude.DegradedMessage ??= message;
                break;
            case "antigravity":
                Gemini.DegradedMessage ??= message;
                break;
            case "openrouter":
                OpenRouter.DegradedMessage = message;
                break;
            case "opencode":
                OpenCode.DegradedMessage = message;
                break;
        }
    }
}

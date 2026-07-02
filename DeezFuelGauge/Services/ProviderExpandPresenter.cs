namespace DeezFuelGauge.Services;

public enum ProviderSection
{
    Cursor,
    OpenAi,
    Gemini,
    OpenRouter,
    OpenCode
}

public readonly record struct ProviderExpandState(
    bool Cursor,
    bool OpenAi,
    bool Gemini,
    bool OpenRouter,
    bool OpenCode)
{
    public static ProviderExpandState None => new(false, false, false, false, false);

    public static ProviderExpandState ExpandOnly(ProviderSection section) => section switch
    {
        ProviderSection.Cursor => new(true, false, false, false, false),
        ProviderSection.OpenAi => new(false, true, false, false, false),
        ProviderSection.Gemini => new(false, false, true, false, false),
        ProviderSection.OpenRouter => new(false, false, false, true, false),
        ProviderSection.OpenCode => new(false, false, false, false, true),
        _ => None
    };
}

public static class ProviderExpandPresenter
{
    public static ProviderExpandState Toggle(
        ProviderSection section,
        ProviderExpandState current)
    {
        var wasExpanded = IsExpanded(section, current);
        var expanding = !wasExpanded;

        if (expanding)
            current = ProviderExpandState.None;

        return section switch
        {
            ProviderSection.Cursor => current with { Cursor = expanding },
            ProviderSection.OpenAi => current with { OpenAi = expanding },
            ProviderSection.Gemini => current with { Gemini = expanding },
            ProviderSection.OpenRouter => current with { OpenRouter = expanding },
            ProviderSection.OpenCode => current with { OpenCode = expanding },
            _ => current
        };
    }

    private static bool IsExpanded(ProviderSection section, ProviderExpandState state) =>
        section switch
        {
            ProviderSection.Cursor => state.Cursor,
            ProviderSection.OpenAi => state.OpenAi,
            ProviderSection.Gemini => state.Gemini,
            ProviderSection.OpenRouter => state.OpenRouter,
            ProviderSection.OpenCode => state.OpenCode,
            _ => false
        };
}

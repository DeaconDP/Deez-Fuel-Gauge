using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class ProviderExpandPresenterTests
{
    [Fact]
    public void Toggle_expands_collapsed_provider()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.OpenAi,
            ProviderExpandState.None);

        Assert.False(state.Cursor);
        Assert.True(state.OpenAi);
        Assert.False(state.Claude);
        Assert.False(state.Gemini);
        Assert.False(state.OpenRouter);
        Assert.False(state.OpenCode);
    }

    [Fact]
    public void Toggle_collapses_expanded_provider()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.OpenAi,
            new ProviderExpandState(false, true, false, false, false, false));

        Assert.False(state.OpenAi);
    }

    [Fact]
    public void Toggle_accordion_collapses_other_providers_when_expanding()
    {
        var state = ProviderExpandPresenter.Toggle(
            ProviderSection.Claude,
            new ProviderExpandState(true, false, false, false, false, false));

        Assert.False(state.Cursor);
        Assert.False(state.OpenAi);
        Assert.True(state.Claude);
        Assert.False(state.Gemini);
    }

    [Fact]
    public void ExpandOnly_enables_single_provider()
    {
        var state = ProviderExpandState.ExpandOnly(ProviderSection.Gemini);

        Assert.False(state.Cursor);
        Assert.False(state.OpenAi);
        Assert.False(state.Claude);
        Assert.True(state.Gemini);
        Assert.False(state.OpenRouter);
        Assert.False(state.OpenCode);
    }
}

using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class ModelProviderClassifierTests
{
    [Theory]
    [InlineData("gpt-4.1", ModelProvider.OpenAi)]
    [InlineData("o3-mini", ModelProvider.OpenAi)]
    [InlineData("claude-4.6-opus-high-thinking", ModelProvider.Claude)]
    [InlineData("gemini-2.5-pro", ModelProvider.Gemini)]
    [InlineData("composer-2", ModelProvider.Unknown)]
    [InlineData("default", ModelProvider.Unknown)]
    public void Classify_maps_model_names_to_provider(string model, ModelProvider expected)
    {
        Assert.Equal(expected, ModelProviderClassifier.Classify(model));
    }
}

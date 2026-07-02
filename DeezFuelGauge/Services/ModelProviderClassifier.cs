namespace DeezFuelGauge.Services;

public enum ModelProvider
{
    Unknown,
    OpenAi,
    Gemini
}

public static class ModelProviderClassifier
{
    public static ModelProvider Classify(string? modelName)
    {
        if (string.IsNullOrWhiteSpace(modelName))
            return ModelProvider.Unknown;

        var model = modelName.Trim().ToLowerInvariant();

        if (model.StartsWith("gemini") || model.Contains("google"))
            return ModelProvider.Gemini;

        if (model.StartsWith("gpt")
            || model.StartsWith("o1")
            || model.StartsWith("o3")
            || model.StartsWith("o4")
            || model.StartsWith("chatgpt")
            || model.StartsWith("codex"))
            return ModelProvider.OpenAi;

        return ModelProvider.Unknown;
    }
}

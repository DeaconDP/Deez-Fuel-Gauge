using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services.Providers;

public interface IProviderUsageAdapter<TSnapshot>
{
    string ProviderKey { get; }
    Task<TSnapshot> FetchAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexUsageAdapter : IProviderUsageAdapter<CodexSnapshot>
{
    private readonly CodexUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public CodexUsageAdapter(CodexUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "codex";

    public Task<CodexSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class ClaudeProUsageAdapter : IProviderUsageAdapter<ClaudeProSnapshot>
{
    private readonly ClaudeProUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public ClaudeProUsageAdapter(ClaudeProUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "claude-pro";

    public Task<ClaudeProSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class AntigravityUsageAdapter : IProviderUsageAdapter<AntigravitySnapshot>
{
    private readonly AntigravityUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public AntigravityUsageAdapter(AntigravityUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "antigravity";

    public Task<AntigravitySnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class OpenRouterUsageAdapter : IProviderUsageAdapter<OpenRouterSnapshot>
{
    private readonly OpenRouterUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public OpenRouterUsageAdapter(OpenRouterUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "openrouter";

    public Task<OpenRouterSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

public sealed class OpenCodeUsageAdapter : IProviderUsageAdapter<OpenCodeSnapshot>
{
    private readonly OpenCodeUsageClient _client;
    private readonly ProviderBillingSettings _settings;

    public OpenCodeUsageAdapter(OpenCodeUsageClient client, ProviderBillingSettings settings)
    {
        _client = client;
        _settings = settings;
    }

    public string ProviderKey => "opencode";

    public Task<OpenCodeSnapshot> FetchAsync(CancellationToken cancellationToken = default) =>
        _client.FetchAsync(_settings, cancellationToken);
}

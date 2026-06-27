using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using CursorUsageWidget.Services.Credentials;
using CursorUsageWidget.Services.Providers;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class CredentialProtectorTests
{
    [Fact]
    public void Protect_unprotect_round_trips_plaintext()
    {
        var original = "sk-test-secret-value"u8.ToArray();
        var protectedBytes = CredentialProtector.Protect(original);
        var restored = CredentialProtector.Unprotect(protectedBytes);

        Assert.Equal(original, restored);
    }
}

public sealed class UsageRefreshServiceTests
{
    [Fact]
    public async Task RefreshAsync_reports_cursor_failure()
    {
        using var refresh = new UsageRefreshService(
            new UsageClient(new HttpClient(new AlwaysNotFoundHandler())),
            new DirectBillingService());

        var result = await refresh.RefreshAsync(new WidgetSettings());

        Assert.False(result.CursorFetchSucceeded);
        Assert.True(result.Snapshot.IsError);
    }

    private sealed class AlwaysNotFoundHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotFound));
    }
}

public sealed class ProviderHealthPresenterTests
{
    [Fact]
    public void FormatDegradedMessage_uses_provider_label()
    {
        var message = ProviderHealthPresenter.FormatDegradedMessage("codex", "timeout");
        Assert.Contains("Codex", message);
        Assert.Contains("unavailable", message);
    }
}

public sealed class ProviderUsageAdapterTests
{
    [Fact]
    public void CodexUsageAdapter_exposes_provider_key()
    {
        var adapter = new CodexUsageAdapter(new CodexUsageClient(), new ProviderBillingSettings());
        Assert.Equal("codex", adapter.ProviderKey);
    }
}

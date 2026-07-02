using System.Net.Http;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeOAuthCallbackListenerTests
{
    [Fact]
    public void ParseRequestLine_extracts_code_and_state()
    {
        var result = ClaudeOAuthCallbackListener.ParseRequestLine(
            "GET /callback?code=abc123&state=xyz789 HTTP/1.1");

        Assert.NotNull(result);
        Assert.Equal("abc123", result!.Code);
        Assert.Equal("xyz789", result.State);
        Assert.Null(result.Error);
    }

    [Fact]
    public void ParseRequestLine_extracts_error()
    {
        var result = ClaudeOAuthCallbackListener.ParseRequestLine(
            "GET /callback?error=access_denied&state=xyz HTTP/1.1");

        Assert.NotNull(result);
        Assert.Null(result!.Code);
        Assert.Equal("access_denied", result.Error);
    }

    [Theory]
    [InlineData("GET /favicon.ico HTTP/1.1")]
    [InlineData("GET /callback HTTP/1.1")]
    [InlineData("POST /callback?code=x HTTP/1.1")]
    public void ParseRequestLine_ignores_unrelated_requests(string requestLine)
    {
        Assert.Null(ClaudeOAuthCallbackListener.ParseRequestLine(requestLine));
    }

    [Fact]
    public async Task WaitForCallbackAsync_returns_code_from_browser_redirect()
    {
        using var listener = ClaudeOAuthCallbackListener.TryStart(0 + 3119) // avoid clashing with the app's default port
            ?? throw new InvalidOperationException("port unavailable");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var waitTask = listener.WaitForCallbackAsync(cts.Token);

        using var http = new HttpClient();
        var response = await http.GetAsync(
            $"http://localhost:{listener.Port}/callback?code=the-code&state=the-state",
            cts.Token);
        var body = await response.Content.ReadAsStringAsync(cts.Token);

        var result = await waitTask;
        Assert.Equal("the-code", result.Code);
        Assert.Equal("the-state", result.State);
        Assert.Contains("Signed in", body);
    }
}

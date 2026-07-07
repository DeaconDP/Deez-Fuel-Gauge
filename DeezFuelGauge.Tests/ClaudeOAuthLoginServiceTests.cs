using System.Net;
using System.Text;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeOAuthLoginServiceTests
{
    [Fact]
    public void BeginLogin_builds_authorize_url_with_pkce_and_state()
    {
        using var service = new ClaudeOAuthLoginService(new HttpClient(new StubHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK))));

        var start = service.BeginLogin();

        Assert.StartsWith("https://claude.com/cai/oauth/authorize?", start.AuthorizeUrl);
        Assert.Contains("code=true", start.AuthorizeUrl);
        Assert.Contains("org%3Acreate_api_key", start.AuthorizeUrl);
        Assert.Contains("client_id=9d1c250a-e61b-44d9-88ed-5944d1962f5e", start.AuthorizeUrl);
        Assert.Contains("code_challenge_method=S256", start.AuthorizeUrl);
        Assert.Contains($"state={start.State}", start.AuthorizeUrl);
        Assert.False(string.IsNullOrWhiteSpace(start.CodeVerifier));
        Assert.False(string.IsNullOrWhiteSpace(start.State));
    }

    [Theory]
    [InlineData("abc123#xyz789", "abc123", "xyz789")]
    [InlineData("abc123", "abc123", "")]
    [InlineData("  abc123#xyz789  ", "abc123", "xyz789")]
    public void SplitPastedCode_separates_code_and_state(string pasted, string expectedCode, string expectedState)
    {
        var (code, state) = ClaudeOAuthLoginService.SplitPastedCode(pasted);

        Assert.Equal(expectedCode, code);
        Assert.Equal(expectedState, state);
    }

    [Fact]
    public async Task ExchangeCodeAsync_rejects_mismatched_state()
    {
        using var service = new ClaudeOAuthLoginService(new HttpClient(new StubHttpHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK))));

        var ex = await Assert.ThrowsAsync<ClaudeOAuthException>(() =>
            service.ExchangeCodeAsync("code#wrong-state", "verifier", "expected-state"));

        Assert.Contains("doesn't match", ex.Message);
    }

    [Fact]
    public async Task ExchangeCodeAsync_posts_pkce_verifier_and_parses_tokens()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new StubHttpHandler(request =>
        {
            capturedRequest = request;
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"sk-ant-oat01-abc","refresh_token":"sk-ant-ort01-def","expires_in":28800}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var service = new ClaudeOAuthLoginService(new HttpClient(handler));

        var token = await service.ExchangeCodeAsync("authcode#matching-state", "verifier-value", "matching-state");

        Assert.Equal("https://platform.claude.com/v1/oauth/token", capturedRequest!.RequestUri!.ToString());
        Assert.Contains("code=authcode", capturedBody);
        Assert.Contains("code_verifier=verifier-value", capturedBody);
        Assert.Contains("grant_type=authorization_code", capturedBody);
        Assert.Equal("sk-ant-oat01-abc", token.AccessToken);
        Assert.Equal("sk-ant-ort01-def", token.RefreshToken);
        Assert.False(token.IsExpired);
    }

    [Fact]
    public async Task RefreshAsync_posts_refresh_grant_and_parses_tokens()
    {
        string? capturedBody = null;
        var handler = new StubHttpHandler(request =>
        {
            capturedBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"access_token":"new-access","refresh_token":"new-refresh","expires_in":3600}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        using var service = new ClaudeOAuthLoginService(new HttpClient(handler));

        var token = await service.RefreshAsync("old-refresh-token");

        Assert.Contains("grant_type=refresh_token", capturedBody);
        Assert.Contains("refresh_token=old-refresh-token", capturedBody);
        Assert.Equal("new-access", token.AccessToken);
    }

    [Fact]
    public async Task RefreshAsync_surfaces_error_description_from_response()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"error":"invalid_grant","error_description":"Authorization code expired"}""",
                Encoding.UTF8,
                "application/json")
        });

        using var service = new ClaudeOAuthLoginService(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ClaudeOAuthException>(() =>
            service.RefreshAsync("expired-refresh-token"));

        Assert.Equal("Authorization code expired", ex.Message);
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }
}

using System.Net;
using System.Text;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderEasySetupServiceTests
{
    private const string SampleAuthJson = """
        {
          "auth_mode": "chatgpt",
          "tokens": {
            "access_token": "eyJhbGciOiJub25lIn0.eyJjaGF0Z3B0X2FjY291bnRfaWQiOiJhY2MtMTIzIn0.",
            "account_id": "acc-from-file"
          }
        }
        """;

    private const string SampleUsageJson = """
        {
          "plan_type": "plus",
          "rate_limit": {
            "limit_reached": false,
            "primary_window": { "used_percent": 2.0, "reset_after_seconds": 7200 },
            "secondary_window": { "used_percent": 1.0, "reset_after_seconds": 432000 }
          },
          "credits": { "balance": "0" }
        }
        """;

    [Fact]
    public void SetupCursor_enables_toggles_and_reports_connected_session()
    {
        var settings = new WidgetSettings { ShowBreakdown = false };
        var service = new ProviderEasySetupService(
            cursorTokenReader: () => new CursorTokens { AccessToken = "token" });

        var result = service.SetupCursor(settings);

        Assert.True(settings.Cursor.ShowCursorSource);
        Assert.True(settings.Cursor.ShowDetails);
        Assert.True(settings.ShowBreakdown);
        Assert.Equal("Connected via Cursor session", result.StatusMessage);
    }

    [Fact]
    public void SetupCursor_reports_missing_cursor_session()
    {
        var settings = new WidgetSettings();
        var service = new ProviderEasySetupService(
            cursorTokenReader: () => new CursorTokens());

        var result = service.SetupCursor(settings);

        Assert.Equal("Sign in to Cursor IDE on this machine", result.StatusMessage);
    }

    [Fact]
    public async Task SetupOpenAi_enables_subscription_toggles_and_tests_codex_auth_file()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-easy-setup-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(authPath, SampleAuthJson);

        try
        {
            var settings = new WidgetSettings { OpenAi = { ShowDirectSource = false } };
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleUsageJson, Encoding.UTF8, "application/json")
            });
            var codex = new CodexUsageClient(new HttpClient(handler), () => authPath);
            var launcher = new RecordingLauncher();
            var service = new ProviderEasySetupService(codex: codex, launcher: launcher);

            var result = await service.SetupOpenAiAsync(settings);

            Assert.True(settings.OpenAi.ShowCursorSource);
            Assert.True(settings.OpenAi.ShowDetails);
            Assert.True(settings.OpenAi.ShowProLimits);
            Assert.False(settings.OpenAi.ShowDirectSource);
            Assert.Equal("Connected (Plus)", result.StatusMessage);
            Assert.Equal("Codex: Connected (Plus)", settings.OpenAi.ProLastConnectionStatus);
            Assert.False(launcher.LaunchedCodexLogin);
            Assert.Empty(launcher.OpenedUrls);
        }
        finally
        {
            File.Delete(authPath);
        }
    }

    [Fact]
    public async Task SetupOpenAi_opens_chatgpt_when_auth_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var codex = new CodexUsageClient(new HttpClient(new StubHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))), () => null);
        var service = new ProviderEasySetupService(codex: codex, launcher: launcher);

        var result = await service.SetupOpenAiAsync(settings);

        Assert.False(launcher.LaunchedCodexLogin);
        Assert.Contains("https://chatgpt.com", launcher.OpenedUrls);
        Assert.False(result.LaunchedCodexLogin);
        Assert.True(result.OpenedExternalUrl);
        Assert.Contains("session cookie", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetupClaude_opens_claude_when_auth_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var service = new ProviderEasySetupService(
            claudePro: new ClaudeProUsageClient(
                new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
                new ClaudeProAuthResolver(
                    claudeCodeReader: () => null,
                    browserCookieReader: () => null,
                    savedSessionReader: _ => null)),
            launcher: launcher);

        var result = await service.SetupClaudeAsync(settings);

        Assert.True(settings.Claude.ShowProLimits);
        Assert.False(settings.Claude.ShowApiConsoleBilling);
        Assert.Contains("https://claude.ai/settings/usage", launcher.OpenedUrls);
        Assert.Contains("Refresh", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetupClaude_tests_existing_session_cookie()
    {
        var settings = new WidgetSettings
        {
            Claude = { ProSessionCredentialId = CredentialStore.Store("claude-pro-test", "session-key") }
        };

        try
        {
            var handler = new StubHttpHandler(request =>
            {
                if (request.RequestUri!.AbsolutePath == "/api/account")
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            """{"memberships":[{"organization":{"uuid":"org-1"}}]}""",
                            Encoding.UTF8,
                            "application/json")
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"five_hour":{"utilization":0.1},"seven_day":{"utilization":20}}""",
                        Encoding.UTF8,
                        "application/json")
                };
            });

            var service = new ProviderEasySetupService(
                claudePro: new ClaudeProUsageClient(new HttpClient(handler)),
                launcher: new RecordingLauncher());

            var result = await service.SetupClaudeAsync(settings);

            Assert.Equal("Connected", result.StatusMessage);
        }
        finally
        {
            CredentialStore.Delete(settings.Claude.ProSessionCredentialId);
        }
    }

    [Fact]
    public async Task SetupClaude_connects_via_claude_code_oauth()
    {
        var handler = new StubHttpHandler(request =>
        {
            Assert.Equal("/api/oauth/usage", request.RequestUri!.AbsolutePath);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.5},"seven_day":{"utilization":0.6}}""",
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var launcher = new RecordingLauncher();
        var service = new ProviderEasySetupService(
            claudePro: new ClaudeProUsageClient(
                new HttpClient(handler),
                new ClaudeProAuthResolver(
                    claudeCodeReader: () => new ClaudeCodeOAuthCredential { AccessToken = "oauth-token" },
                    browserCookieReader: () => null,
                    savedSessionReader: _ => null)),
            launcher: launcher);

        var result = await service.SetupClaudeAsync(new WidgetSettings());

        Assert.Equal("Connected", result.StatusMessage);
        Assert.Empty(launcher.OpenedUrls);
    }

    [Fact]
    public async Task SetupGemini_opens_antigravity_when_tokens_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var service = new ProviderEasySetupService(
            antigravity: new AntigravityUsageClient(
                new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
                () => new AntigravityOAuthTokens()),
            launcher: launcher,
            antigravityTokenReader: () => new AntigravityOAuthTokens());

        var result = await service.SetupGeminiAsync(settings);

        Assert.True(settings.Gemini.ShowProLimits);
        Assert.Contains("https://antigravity.google/", launcher.OpenedUrls);
        Assert.Equal("Sign in to Antigravity on this machine", result.StatusMessage);
    }

    [Fact]
    public void SetupDisk_enables_disk_sections()
    {
        var settings = new WidgetSettings { ShowDiskDrives = false, ShowDiskDetails = false };
        var service = new ProviderEasySetupService();

        var result = service.SetupDisk(settings);

        Assert.True(settings.ShowDiskDrives);
        Assert.True(settings.ShowDiskDetails);
        Assert.Equal("Disk drives enabled", result.StatusMessage);
    }

    [Fact]
    public void TryReadLocalAuthFile_reads_valid_auth_json()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-probe-{Guid.NewGuid():N}.json");
        File.WriteAllText(authPath, SampleAuthJson);

        try
        {
            var found = CodexUsageClient.TryReadLocalAuthFile(out var auth, () => authPath);

            Assert.True(found);
            Assert.NotNull(auth);
            Assert.Equal("acc-from-file", auth.Value.AccountId);
        }
        finally
        {
            File.Delete(authPath);
        }
    }

    private sealed class RecordingLauncher : ExternalSetupLauncher
    {
        public bool LaunchedCodexLogin { get; private set; }

        public List<string> OpenedUrls { get; } = [];

        public override void OpenUrl(string url) => OpenedUrls.Add(url);

        public override void LaunchCodexLogin() => LaunchedCodexLogin = true;
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

using System.Net;
using System.Text;
using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

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
        Assert.Equal("Connected via Cursor session", settings.Cursor.LastConnectionStatus);
    }

    [Fact]
    public void SetupCursor_launches_ide_when_session_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var service = new ProviderEasySetupService(
            cursorTokenReader: () => new CursorTokens(),
            launcher: launcher);

        var result = service.SetupCursor(settings);

        Assert.True(launcher.LaunchedCursorIde);
        Assert.Contains("Test", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(result.StatusMessage, settings.Cursor.LastConnectionStatus);
    }

    [Fact]
    public async Task SetupOpenAi_enables_cursor_spend_only()
    {
        var settings = new WidgetSettings { OpenAi = { ShowDirectSource = true, ShowProLimits = false } };
        var service = new ProviderEasySetupService(
            cursorTokenReader: () => new CursorTokens { AccessToken = "token" });

        var result = await service.SetupOpenAiAsync(settings);

        Assert.True(settings.OpenAi.ShowCursorSource);
        Assert.True(settings.OpenAi.ShowDetails);
        Assert.True(settings.OpenAi.ShowDirectSource);
        Assert.False(settings.OpenAi.ShowProLimits);
        Assert.Equal("Via Cursor: connected", result.StatusMessage);
    }

    [Fact]
    public async Task SetupCodex_enables_codex_and_tests_auth_file()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-easy-setup-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(authPath, SampleAuthJson);

        try
        {
            var settings = new WidgetSettings { OpenAi = { ShowCursorSource = false } };
            var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleUsageJson, Encoding.UTF8, "application/json")
            });
            var codex = new CodexUsageClient(new HttpClient(handler), () => authPath);
            var launcher = new RecordingLauncher();
            var service = new ProviderEasySetupService(codex: codex, launcher: launcher);

            var result = await service.SetupCodexAsync(settings);

            Assert.False(settings.OpenAi.ShowCursorSource);
            Assert.True(settings.OpenAi.ShowProLimits);
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
    public async Task SetupCodex_launches_codex_login_when_auth_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var codex = new CodexUsageClient(new HttpClient(new StubHttpHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK))), () => null);
        var emptyResolver = new CodexAuthResolver(
            authFileReader: () => null,
            browserCookieReader: () => null,
            savedSessionReader: _ => null);
        var service = new ProviderEasySetupService(codex: codex, launcher: launcher, codexAuthResolver: emptyResolver);

        var result = await service.SetupCodexAsync(settings);

        Assert.True(launcher.LaunchedCodexLogin);
        Assert.Contains("https://chatgpt.com", launcher.OpenedUrls);
        Assert.True(result.LaunchedCodexLogin);
        Assert.True(result.OpenedExternalUrl);
        Assert.Contains("codex login", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }
    [Fact]
    public async Task SetupGemini_launches_antigravity_ide_when_tokens_missing()
    {
        var settings = new WidgetSettings();
        var launcher = new RecordingLauncher();
        var service = new ProviderEasySetupService(
            antigravity: new AntigravityUsageClient(
                new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK))),
                new GeminiAuthResolver(
                    antigravityReader: () => new AntigravityOAuthTokens(),
                    geminiCliReader: () => new AntigravityOAuthTokens())),
            launcher: launcher,
            geminiAuthResolver: new GeminiAuthResolver(
                antigravityReader: () => new AntigravityOAuthTokens(),
                geminiCliReader: () => new AntigravityOAuthTokens()));

        var result = await service.SetupGeminiAsync(settings);

        Assert.True(settings.Gemini.ShowProLimits);
        Assert.True(launcher.LaunchedAntigravityIde);
        Assert.Contains("Antigravity IDE", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetupGemini_tests_connection_when_cli_auth_present()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("loadCodeAssist", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"cloudaicompanionProject":"proj-1","currentTier":{"id":"standard-tier"}}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AntigravityUsageClientTests.SampleQuotaSummaryJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var authResolver = new GeminiAuthResolver(
            antigravityReader: () => new AntigravityOAuthTokens(),
            geminiCliReader: () => new AntigravityOAuthTokens { AccessToken = "cli-token" });

        var service = new ProviderEasySetupService(
            antigravity: new AntigravityUsageClient(new HttpClient(handler), authResolver),
            geminiAuthResolver: authResolver);

        var result = await service.SetupGeminiAsync(new WidgetSettings());

        Assert.StartsWith("Connected", result.StatusMessage, StringComparison.OrdinalIgnoreCase);
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

        public bool LaunchedAntigravityIde { get; private set; }

        public bool LaunchedCursorIde { get; private set; }

        public List<string> OpenedUrls { get; } = [];

        public override void OpenUrl(string url) => OpenedUrls.Add(url);

        public override bool TryLaunchCodexLogin()
        {
            LaunchedCodexLogin = true;
            return true;
        }

        public override AppLaunchResult LaunchAntigravityIde()
        {
            LaunchedAntigravityIde = true;
            return AppLaunchResult.Launched;
        }

        public override AppLaunchResult LaunchCursorIde()
        {
            LaunchedCursorIde = true;
            return AppLaunchResult.Launched;
        }
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

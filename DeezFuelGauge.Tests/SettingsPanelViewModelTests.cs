using System.Net;
using System.Text;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using DeezFuelGauge.Settings;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class SettingsPanelViewModelTests
{
    [Fact]
    public void Load_and_Commit_round_trip_preserves_provider_toggles()
    {
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false, ShowDetails = false },
            OpenAi = new ProviderBillingSettings
            {
                ShowCursorSource = true,
                ShowDirectSource = true,
                ShowProLimits = false,
                ShowProBreakdown = false,
                OrganizationId = "org-1",
                MonthlyBudgetUsd = 100
            },
            Claude = new ProviderBillingSettings
            {
                ShowProLimits = true,
                ShowProDetails = false,
                ShowApiConsoleBilling = true
            },
            Gemini = new ProviderBillingSettings
            {
                ShowProLimits = false,
                ShowProBreakdown = true
            },
            ShowBreakdown = false,
            ShowDiskDrives = false
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        Assert.False(viewModel.ShowCursor);
        Assert.True(viewModel.ShowOpenAiDirect);
        Assert.False(viewModel.ShowCodexLimits);
        Assert.Equal("org-1", viewModel.OpenAiOrgId);
        Assert.Equal("100", viewModel.OpenAiBudget);
        Assert.False(viewModel.ShowClaudeProDetails);
        Assert.False(viewModel.ShowAntigravityLimits);

        viewModel.ShowCursor = true;
        viewModel.ShowCodexLimits = true;
        viewModel.ShowAntigravityLimits = true;
        viewModel.OpenAiOrgId = "org-2";

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.True(committed.Cursor.ShowCursorSource);
        Assert.True(committed.OpenAi.ShowProLimits);
        Assert.True(committed.Gemini.ShowProLimits);
        Assert.Equal("org-2", committed.OpenAi.OrganizationId);
        Assert.Equal(100, committed.OpenAi.MonthlyBudgetUsd);
        Assert.False(committed.Claude.EffectiveShowProDetails);
    }

    [Fact]
    public void Commit_persists_disabled_claude_pro_and_antigravity_limits()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings
        {
            Claude = new ProviderBillingSettings { ShowProLimits = true },
            Gemini = new ProviderBillingSettings { ShowProLimits = true }
        });

        viewModel.ShowClaudePro = false;
        viewModel.ShowAntigravityLimits = false;

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.False(committed.Claude.ShowProLimits);
        Assert.False(committed.Gemini.ShowProLimits);
    }

    [Fact]
    public void ToggleExpandedProvider_collapses_other_sections()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());

        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.OpenAi);

        Assert.True(viewModel.IsOpenAiExpanded);
        Assert.False(viewModel.IsCursorExpanded);

        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.OpenAi);

        Assert.False(viewModel.IsOpenAiExpanded);
    }

    [Fact]
    public void ShowCodexCredentials_hidden_when_auth_file_present()
    {
        var authPath = Path.Combine(Path.GetTempPath(), $"codex-vm-{Guid.NewGuid():N}.json");
        File.WriteAllText(authPath, """
            {
              "auth_mode": "chatgpt",
              "tokens": { "access_token": "tok", "account_id": "acc" }
            }
            """);

        try
        {
            var viewModel = new SettingsPanelViewModel(
                new ProviderEasySetupService(),
                new OpenAiBillingClient(),
                new CodexUsageClient(authFilePathResolver: () => authPath),
                new AnthropicBillingClient(),
                new ClaudeProUsageClient(),
                new AntigravityUsageClient(),
                new OpenRouterUsageClient(),
                new OpenCodeUsageClient(),
                () => new CursorTokens());

            viewModel.Load(new WidgetSettings());
            viewModel.ShowCodexLimits = true;

            Assert.False(viewModel.ShowCodexCredentials);
        }
        finally
        {
            File.Delete(authPath);
        }
    }

    [Fact]
    public void Commit_persists_expanded_provider()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());
        viewModel.ToggleExpandedProvider(SettingsExpandedProvider.Claude);

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.Equal(SettingsExpandedProvider.Claude, committed.SettingsExpandedProvider);
    }

    [Fact]
    public void ShowOpenRouterCredentials_hidden_when_limits_off_or_key_saved()
    {
        var viewModel = CreateViewModel();
        viewModel.Load(new WidgetSettings());

        viewModel.ShowOpenRouterLimits = true;
        Assert.True(viewModel.ShowOpenRouterCredentials);

        viewModel.SaveOpenRouterApiKey("sk-or-test");
        Assert.False(viewModel.ShowOpenRouterCredentials);

        viewModel.ClearOpenRouterApiKey();
        viewModel.ShowOpenRouterLimits = false;
        Assert.False(viewModel.ShowOpenRouterCredentials);
    }

    [Fact]
    public void Load_and_Commit_round_trip_preserves_opencode_go_details()
    {
        var settings = new WidgetSettings
        {
            OpenCode = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                ShowProLimits = true,
                ShowDetails = true,
                ShowProDetails = false,
                ShowProBreakdown = false
            }
        };

        var viewModel = CreateViewModel();
        viewModel.Load(settings);

        Assert.True(viewModel.ShowOpenCodeZenDetails);
        Assert.False(viewModel.ShowOpenCodeGoDetails);

        viewModel.ShowOpenCodeGoDetails = true;
        viewModel.ShowOpenCodeGoBreakdown = true;

        var committed = new WidgetSettings();
        viewModel.Commit(committed);

        Assert.True(committed.OpenCode.ShowProDetails);
        Assert.True(committed.OpenCode.EffectiveShowProDetails);
        Assert.True(committed.OpenCode.ShowProBreakdown);
    }

    [Fact]
    public async Task RunEasySetupClaudeAsync_enables_claude_pro_limits()
    {
        var claudePro = new ClaudeProUsageClient(
            new HttpClient(new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized))),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                browserCookieReader: () => null,
                savedSessionReader: _ => null));

        var service = new ProviderEasySetupService(claudePro: claudePro);

        var viewModel = new SettingsPanelViewModel(
            service,
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AnthropicBillingClient(),
            claudePro,
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            () => new CursorTokens());

        var settings = new WidgetSettings
        {
            Claude = new ProviderBillingSettings { ShowProLimits = false }
        };

        viewModel.Load(settings);
        await viewModel.RunEasySetupClaudeAsync(settings);

        Assert.True(settings.Claude.ShowCursorSource);
        Assert.True(settings.Claude.ShowDetails);
        Assert.True(settings.Claude.ShowProLimits);
        Assert.Contains("Sign in at claude.ai", viewModel.ClaudeProStatus);
    }

    [Fact]
    public void ClaudeCursorPlanStatus_reflects_cursor_session()
    {
        var viewModel = CreateViewModel(() => new CursorTokens { AccessToken = "token" });
        viewModel.Load(new WidgetSettings());
        Assert.Equal("Connected via Cursor session", viewModel.ClaudeCursorPlanStatus);
    }

    [Fact]
    public void OpenAiCursorPlanStatus_reflects_cursor_session()
    {
        var viewModel = CreateViewModel(() => new CursorTokens { AccessToken = "token" });
        viewModel.Load(new WidgetSettings());
        Assert.Equal("Connected via Cursor session", viewModel.OpenAiCursorPlanStatus);
    }

    [Fact]
    public void GeminiCursorPlanStatus_reflects_cursor_session()
    {
        var viewModel = CreateViewModel(() => new CursorTokens { AccessToken = "token" });
        viewModel.Load(new WidgetSettings());
        Assert.Equal("Connected via Cursor session", viewModel.GeminiCursorPlanStatus);
    }

    [Fact]
    public void CursorPlanStatus_without_token_prompts_sign_in()
    {
        var viewModel = CreateViewModel(() => new CursorTokens());
        viewModel.Load(new WidgetSettings());
        Assert.Equal("Sign in to Cursor IDE on this machine", viewModel.OpenAiCursorPlanStatus);
        Assert.Equal("Sign in to Cursor IDE on this machine", viewModel.GeminiCursorPlanStatus);
    }

    private static SettingsPanelViewModel CreateViewModel(Func<CursorTokens> cursorReader)
    {
        return new SettingsPanelViewModel(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AnthropicBillingClient(),
            new ClaudeProUsageClient(),
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            cursorReader);
    }

    [Fact]
    public async Task RefreshClaudeProAsync_updates_status_and_persists_session()
    {
        var handler = new StubHttpHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/api/account")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"memberships":[{"organization":{"uuid":"org-1"}}]}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"five_hour":{"utilization":0.2},"seven_day":{"utilization":0.3}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        var claudePro = new ClaudeProUsageClient(
            new HttpClient(handler),
            new ClaudeProAuthResolver(
                claudeCodeReader: () => null,
                browserCookieReader: () => "browser-session",
                savedSessionReader: _ => null));

        var viewModel = new SettingsPanelViewModel(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AnthropicBillingClient(),
            claudePro,
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            () => new CursorTokens());

        var settings = new WidgetSettings();

        try
        {
            await viewModel.RefreshClaudeProAsync(settings);

            Assert.Equal("Connected", viewModel.ClaudeProStatus);
            Assert.True(viewModel.HasClaudeSessionCookieSaved);
            Assert.Equal("browser-session", CredentialStore.Retrieve(settings.Claude.ProSessionCredentialId));
        }
        finally
        {
            CredentialStore.Delete(settings.Claude.ProSessionCredentialId);
        }
    }

    private static SettingsPanelViewModel CreateViewModel() =>
        new(
            new ProviderEasySetupService(),
            new OpenAiBillingClient(),
            new CodexUsageClient(),
            new AnthropicBillingClient(),
            new ClaudeProUsageClient(),
            new AntigravityUsageClient(),
            new OpenRouterUsageClient(),
            new OpenCodeUsageClient(),
            () => new CursorTokens());

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

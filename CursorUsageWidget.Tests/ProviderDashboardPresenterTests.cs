using CursorUsageWidget.Models;
using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class ProviderDashboardPresenterTests
{
    [Fact]
    public void IsOpenAiDashboardVisible_true_when_direct_or_codex_enabled()
    {
        Assert.False(ProviderDashboardPresenter.IsOpenAiDashboardVisible(new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = false,
            ShowProLimits = false
        }));
        Assert.True(ProviderDashboardPresenter.IsOpenAiDashboardVisible(new ProviderBillingSettings { ShowDirectSource = true }));
        Assert.True(ProviderDashboardPresenter.IsOpenAiDashboardVisible(new ProviderBillingSettings { ShowProLimits = true }));
    }

    [Fact]
    public void IsOpenAiDashboardVisible_false_when_all_sources_disabled()
    {
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = false,
            ShowDirectSource = false,
            ShowProLimits = false
        };

        Assert.False(ProviderDashboardPresenter.IsOpenAiDashboardVisible(settings));
    }

    [Fact]
    public void IsGeminiDashboardVisible_false_when_both_sources_disabled()
    {
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = false,
            ShowProLimits = false
        };

        Assert.False(ProviderDashboardPresenter.IsGeminiDashboardVisible(settings));
    }

    [Fact]
    public void IsCursorDashboardVisible_true_when_any_cursor_source_enabled()
    {
        Assert.False(ProviderDashboardPresenter.IsCursorDashboardVisible(new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            OpenAi = new ProviderBillingSettings { ShowCursorSource = false },
            Gemini = new ProviderBillingSettings { ShowCursorSource = false }
        }));
        Assert.True(ProviderDashboardPresenter.IsCursorDashboardVisible(new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings { ShowCursorSource = true }
        }));
    }

    [Fact]
    public void ComputeCursorHeadline_includes_auto_and_api_when_breakdown_enabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 90,
            ApiPercentUsed = 10
        };
        var settings = new WidgetSettings { ShowBreakdown = true };

        Assert.Equal(90, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_uses_main_percent_when_breakdown_disabled()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 40,
            AutoPercentUsed = 90,
            ApiPercentUsed = 10
        };
        var settings = new WidgetSettings { ShowBreakdown = false };

        Assert.Equal(40, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeCursorHeadline_includes_via_cursor_provider_usage()
    {
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 55 }
        };
        var settings = new WidgetSettings
        {
            Cursor = new ProviderBillingSettings { ShowCursorSource = false },
            OpenAi = new ProviderBillingSettings { ShowCursorSource = true }
        };

        Assert.Equal(55, ProviderDashboardPresenter.ComputeCursorHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeOpenAiHeadline_uses_max_across_enabled_available_sources()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 30 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 75 },
            Codex = CodexSnapshot.FromUsage("plus", 84, 4, null, null, null, false)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.Equal(84, ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeOpenAiHeadline_excludes_disabled_sources()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 30 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 75 },
            Codex = CodexSnapshot.FromUsage("plus", 84, 4, null, null, null, false)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = false,
            ShowProLimits = false
        };

        Assert.Equal(0, ProviderDashboardPresenter.ComputeOpenAiHeadline(snapshot, settings));
    }

    [Fact]
    public void ComputeGeminiHeadline_uses_max_across_cursor_and_antigravity()
    {
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);
        var thirdParty = AntigravityGroupSnapshot.FromUsage(50, 40, null, null);
        var snapshot = new UsageSnapshot
        {
            Gemini = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 25 },
            Antigravity = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty)
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowProLimits = true
        };

        Assert.Equal(60, ProviderDashboardPresenter.ComputeGeminiHeadline(snapshot, settings));
    }

    [Fact]
    public void IsOpenAiHeadlineConnected_true_when_direct_or_codex_available()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = true, PercentUsed = 0 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = true, PercentUsed = 0 },
            Codex = CodexSnapshot.Unavailable("No Codex")
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.True(ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsOpenAiHeadlineConnected_false_when_no_enabled_source_is_available()
    {
        var snapshot = new UsageSnapshot
        {
            OpenAi = new ProviderUsageSnapshot { IsAvailable = false, PercentUsed = 0 },
            OpenAiDirect = new DirectProviderSnapshot { IsAvailable = false, PercentUsed = 0 },
            Codex = CodexSnapshot.Unavailable("No Codex")
        };
        var settings = new ProviderBillingSettings
        {
            ShowCursorSource = true,
            ShowDirectSource = true,
            ShowProLimits = true
        };

        Assert.False(ProviderDashboardPresenter.IsOpenAiHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsOpenRouterHeadlineConnected_false_when_unavailable_even_with_pro_limits_enabled()
    {
        var snapshot = new UsageSnapshot
        {
            OpenRouter = OpenRouterSnapshot.Unavailable("Not connected")
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.False(ProviderDashboardPresenter.IsOpenRouterHeadlineConnected(snapshot, settings));
    }

    [Fact]
    public void IsOpenRouterHeadlineConnected_false_when_openrouter_hidden()
    {
        var snapshot = new UsageSnapshot
        {
            OpenRouter = new OpenRouterSnapshot
            {
                IsAvailable = true,
                HeadlinePercentUsed = 0,
                DetailLabel = "Connected"
            }
        };
        var settings = new ProviderBillingSettings { ShowProLimits = true };

        Assert.False(ProviderDashboardPresenter.IsOpenRouterHeadlineConnected(snapshot, settings));
    }
}

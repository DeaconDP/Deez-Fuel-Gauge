using Avalonia.Controls;
using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ProviderLimitsPresenterTests
{
    [Fact]
    public void HeadlinePercent_uses_max_of_session_and_weekly()
    {
        Assert.Equal(84, ProviderLimitsPresenter.HeadlinePercent(84, 4));
        Assert.Equal(84, ProviderLimitsPresenter.HeadlinePercent(4, 84));
    }

    [Fact]
    public void FormatSessionWeeklySummary_matches_cursor_style()
    {
        var summary = ProviderLimitsPresenter.FormatSessionWeeklySummary(84, 4);

        Assert.Equal("84% 5-hour and 4% weekly used", summary);
    }

    [Fact]
    public void FormatSessionWeeklySummary_omits_missing_windows()
    {
        var weeklyOnly = ProviderLimitsPresenter.FormatSessionWeeklySummary(
            0, 1, hasSessionWindow: false, hasWeeklyWindow: true);

        Assert.Equal("1% weekly used", weeklyOnly);
    }

    [Fact]
    public void FormatCodexFooter_includes_plan_and_credits()
    {
        var snapshot = CodexSnapshot.FromUsage("plus", 12, 34, null, null, 50m, false);

        Assert.Equal("Plus · $50 ChatGPT credits", ProviderLimitsPresenter.FormatCodexFooter(snapshot));
    }

    [Fact]
    public void FormatCodexFooter_omits_credits_when_missing()
    {
        var snapshot = CodexSnapshot.FromUsage("plus", 12, 34, null, null, null, false);

        Assert.Equal("Plus", ProviderLimitsPresenter.FormatCodexFooter(snapshot));
    }

    [Fact]
    public void FormatCodexFooter_empty_when_unavailable()
    {
        var snapshot = CodexSnapshot.Unavailable("Not signed in");

        Assert.Equal("", ProviderLimitsPresenter.FormatCodexFooter(snapshot));
    }

    [Fact]
    public void FormatAntigravitySummary_uses_gemini_and_third_party_5h_and_weekly()
    {
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);
        var thirdParty = AntigravityGroupSnapshot.FromUsage(70, 50, null, null);
        var snapshot = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty);

        Assert.Equal("20% Gemini 5h · 40% Gemini wk · 30% 3P 5h · 50% 3P wk used", ProviderLimitsPresenter.FormatAntigravitySummary(snapshot));
    }

    [Fact]
    public void FormatAntigravitySummary_falls_back_when_unavailable()
    {
        var snapshot = AntigravitySnapshot.Unavailable("No tokens");

        Assert.Equal("No tokens", ProviderLimitsPresenter.FormatAntigravitySummary(snapshot));
    }

    [Fact]
    public void AntigravityHeadlinePercent_uses_max_across_all_buckets()
    {
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);
        var thirdParty = AntigravityGroupSnapshot.FromUsage(50, 40, null, null);
        var snapshot = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty);

        Assert.Equal(60, ProviderLimitsPresenter.AntigravityHeadlinePercent(snapshot));
    }

    [Fact]
    public void FormatAntigravityFooter_returns_plan_label()
    {
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);
        var thirdParty = AntigravityGroupSnapshot.Unavailable();
        var snapshot = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty);

        Assert.Equal("Pro", ProviderLimitsPresenter.FormatAntigravityFooter(snapshot));
    }

    [Fact]
    public void FormatResetTimes_omits_missing_windows()
    {
        var sessionReset = new DateTimeOffset(2026, 6, 24, 14, 30, 0, TimeSpan.Zero);

        var text = ProviderLimitsPresenter.FormatResetTimes(sessionReset, null);

        Assert.StartsWith("5h resets", text);
        Assert.DoesNotContain("weekly resets", text);
    }

    [Fact]
    public void FormatCodexFooter_includes_reset_times_when_present()
    {
        var sessionReset = new DateTimeOffset(2026, 6, 24, 14, 30, 0, TimeSpan.Zero);
        var snapshot = CodexSnapshot.FromUsage("plus", 12, 34, sessionReset, null, null, false);

        var footer = ProviderLimitsPresenter.FormatCodexFooter(snapshot);

        Assert.StartsWith("Plus · 5h resets", footer);
    }

    [Fact]
    public void FormatAntigravityFooter_includes_plan_and_reset_times()
    {
        var sessionReset = new DateTimeOffset(2026, 6, 24, 14, 30, 0, TimeSpan.Zero);
        var gemini = AntigravityGroupSnapshot.FromUsage(80, 60, sessionReset, null);
        var thirdParty = AntigravityGroupSnapshot.Unavailable();
        var snapshot = AntigravitySnapshot.FromGroups("Pro", gemini, thirdParty);

        var footer = ProviderLimitsPresenter.FormatAntigravityFooter(snapshot);

        Assert.StartsWith("Pro · 5h resets", footer);
    }

    [Fact]
    public void HeadlinePercent3_returns_max_of_three_windows()
    {
        Assert.Equal(80, ProviderLimitsPresenter.HeadlinePercent3(10, 80, 40));
    }

    [Fact]
    public void FormatThreeWindowSummary_formats_all_windows()
    {
        var summary = ProviderLimitsPresenter.FormatThreeWindowSummary(10, 20, 30);
        Assert.Contains("10% 5h", summary);
        Assert.Contains("20% wk", summary);
        Assert.Contains("30% mo", summary);
    }

    [Fact]
    public void ApplyBreakdownLayout_shows_nested_panel_and_hides_source_bar()
    {
        var section = new StackPanel();
        var panel = new Border();
        var barBorder = new Border { IsVisible = true };
        var remainingText = new TextBlock();

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            showProBreakdown: true,
            isAvailable: true,
            footerText: "Plus",
            showFooter: true,
            section,
            panel,
            barBorder,
            remainingText);

        Assert.True(section.IsVisible);
        Assert.True(panel.IsVisible);
        Assert.False(barBorder.IsVisible);
        Assert.Equal("Plus", remainingText.Text);
        Assert.True(remainingText.IsVisible);
    }

    [Fact]
    public void ApplyBreakdownLayout_hides_breakdown_and_shows_source_bar_when_unavailable()
    {
        var section = new StackPanel { IsVisible = true };
        var panel = new Border { IsVisible = true };
        var barBorder = new Border { IsVisible = false };
        var remainingText = new TextBlock { Text = "old", IsVisible = true };

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            showProBreakdown: true,
            isAvailable: false,
            footerText: "Plus",
            showFooter: true,
            section,
            panel,
            barBorder,
            remainingText);

        Assert.False(section.IsVisible);
        Assert.False(panel.IsVisible);
        Assert.True(barBorder.IsVisible);
        Assert.Equal("Plus", remainingText.Text);
        Assert.True(remainingText.IsVisible);
    }

    [Fact]
    public void ApplyBreakdownLayout_hides_breakdown_when_showProBreakdown_disabled()
    {
        var section = new StackPanel { IsVisible = true };
        var panel = new Border { IsVisible = true };
        var barBorder = new Border { IsVisible = false };
        var remainingText = new TextBlock();

        ProviderLimitsPresenter.ApplyBreakdownLayout(
            showProBreakdown: false,
            isAvailable: true,
            footerText: "",
            showFooter: false,
            section,
            panel,
            barBorder,
            remainingText);

        Assert.False(section.IsVisible);
        Assert.False(panel.IsVisible);
        Assert.True(barBorder.IsVisible);
    }
}

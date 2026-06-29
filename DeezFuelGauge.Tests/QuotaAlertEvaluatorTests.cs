using DeezFuelGauge.Models;
using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class QuotaAlertEvaluatorTests
{
    private static readonly WidgetSettings DefaultSettings = new();

    [Fact]
    public void Evaluate_cursor_plan_alerts_when_usage_low_and_period_ending_soon()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Single(alerts);
        Assert.Equal("cursor-plan", alerts[0].SourceId);
        Assert.Equal("Cursor plan", alerts[0].Label);
        Assert.Contains("50% used", alerts[0].Message);
        Assert.Contains("3 days", alerts[0].Message);
    }

    [Fact]
    public void Evaluate_cursor_plan_skips_when_usage_above_threshold()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 90,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_cursor_plan_skips_when_outside_alert_window()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-10);
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_respects_source_disabled_in_settings()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { CursorPlan = false }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 50,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_skips_direct_api_when_budget_not_set()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-3);
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                MonthlyBudgetUsd = 0
            }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds(),
            OpenAiDirect = DirectProviderSnapshot.FromBilling(5, 100, 0, 0)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.DoesNotContain(alerts, a => a.SourceId == "openai-platform");
    }

    [Fact]
    public void Evaluate_openai_platform_alerts_when_budget_configured()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-2);
        var settings = new WidgetSettings
        {
            OpenAi = new ProviderBillingSettings
            {
                ShowDirectSource = true,
                MonthlyBudgetUsd = 100
            }
        };
        var snapshot = new UsageSnapshot
        {
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds(),
            OpenAiDirect = DirectProviderSnapshot.FromBilling(20, 100, 0, 0)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Contains(alerts, a => a.SourceId == "openai-platform");
    }

    [Fact]
    public void Evaluate_opencode_go_uses_resets_at_for_period_end()
    {
        var resetsAt = new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var now = resetsAt.AddDays(-2);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { CursorPlan = false },
            OpenCode = new ProviderBillingSettings { ShowProLimits = true }
        };
        var snapshot = new UsageSnapshot
        {
            OpenCode = OpenCodeSnapshot.FromData(
                null,
                null,
                null,
                null,
                null,
                OpenCodeWindowSnapshot.FromUsage(30, resetsAt),
                hasGoSubscription: true)
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        var alert = Assert.Single(alerts);
        Assert.Equal("opencode-go-monthly", alert.SourceId);
        Assert.Equal(2, alert.DaysRemaining);
    }

    [Fact]
    public void Evaluate_uses_utc_month_fallback_when_billing_cycle_missing()
    {
        var now = new DateTimeOffset(2026, 6, 28, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new UsageSnapshot { PercentUsed = 40 };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Single(alerts);
        Assert.Equal("cursor-plan", alerts[0].SourceId);
    }

    [Fact]
    public void Evaluate_returns_empty_when_master_toggle_disabled()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-1);
        var settings = new WidgetSettings
        {
            QuotaAlerts = new QuotaAlertSettings { Enabled = false }
        };
        var snapshot = new UsageSnapshot
        {
            PercentUsed = 10,
            BillingCycleEndMs = periodEnd.ToUnixTimeMilliseconds()
        };

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, settings, now);

        Assert.Empty(alerts);
    }

    [Fact]
    public void Evaluate_skips_error_snapshot()
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-1);
        var snapshot = UsageSnapshot.Error("failed");

        var alerts = QuotaAlertEvaluator.Evaluate(snapshot, DefaultSettings, now);

        Assert.Empty(alerts);
    }

    [Theory]
    [InlineData(7, 3, true)]
    [InlineData(7, 8, false)]
    [InlineData(7, 0, false)]
    public void IsWithinAlertWindow_respects_days_before_end(int daysBefore, int daysUntilEnd, bool expected)
    {
        var periodEnd = new DateTimeOffset(2026, 6, 30, 0, 0, 0, TimeSpan.Zero);
        var now = periodEnd.AddDays(-daysUntilEnd);

        var result = QuotaAlertEvaluator.IsWithinAlertWindow(periodEnd, now, daysBefore);

        Assert.Equal(expected, result);
    }
}

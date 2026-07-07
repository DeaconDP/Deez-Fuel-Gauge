using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class ProviderLimitsPresenter
{
    private static readonly Color ErrorColor = Color.FromRgb(0xFF, 0x98, 0x00);

    public static double HeadlinePercent(double sessionPercentUsed, double weeklyPercentUsed) =>
        Math.Max(sessionPercentUsed, weeklyPercentUsed);

    public static double HeadlinePercent3(double rollingPercentUsed, double weeklyPercentUsed, double monthlyPercentUsed) =>
        Math.Max(rollingPercentUsed, Math.Max(weeklyPercentUsed, monthlyPercentUsed));

    public static double AntigravityHeadlinePercent(AntigravitySnapshot snapshot)
    {
        var values = new List<double>();
        if (snapshot.Gemini.IsAvailable)
        {
            values.Add(snapshot.Gemini.SessionPercentUsed);
            values.Add(snapshot.Gemini.WeeklyPercentUsed);
        }

        if (snapshot.ThirdParty.IsAvailable)
        {
            values.Add(snapshot.ThirdParty.SessionPercentUsed);
            values.Add(snapshot.ThirdParty.WeeklyPercentUsed);
        }

        return values.Count > 0 ? values.Max() : 0;
    }

    public static string FormatSessionWeeklySummary(double sessionPercentUsed, double weeklyPercentUsed)
    {
        var session = Math.Round(sessionPercentUsed);
        var weekly = Math.Round(weeklyPercentUsed);
        return $"{session.ToString(CultureInfo.InvariantCulture)}% 5-hour and {weekly.ToString(CultureInfo.InvariantCulture)}% weekly used";
    }

    public static string FormatThreeWindowSummary(double rollingPercentUsed, double weeklyPercentUsed, double monthlyPercentUsed)
    {
        var rolling = Math.Round(rollingPercentUsed);
        var weekly = Math.Round(weeklyPercentUsed);
        var monthly = Math.Round(monthlyPercentUsed);
        return $"{rolling.ToString(CultureInfo.InvariantCulture)}% 5h · {weekly.ToString(CultureInfo.InvariantCulture)}% wk · {monthly.ToString(CultureInfo.InvariantCulture)}% mo used";
    }

    public static string FormatAntigravitySummary(AntigravitySnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
            return snapshot.StatusMessage ?? snapshot.DetailLabel;

        var gemini5h = Math.Round(snapshot.Gemini.SessionPercentUsed);
        var geminiWeekly = Math.Round(snapshot.Gemini.WeeklyPercentUsed);
        var thirdParty5h = Math.Round(snapshot.ThirdParty.SessionPercentUsed);
        var thirdPartyWeekly = Math.Round(snapshot.ThirdParty.WeeklyPercentUsed);
        return $"{gemini5h.ToString(CultureInfo.InvariantCulture)}% Gemini 5h · {geminiWeekly.ToString(CultureInfo.InvariantCulture)}% Gemini wk · " +
               $"{thirdParty5h.ToString(CultureInfo.InvariantCulture)}% 3P 5h · {thirdPartyWeekly.ToString(CultureInfo.InvariantCulture)}% 3P wk used";
    }

    public static string FormatOpenCodeGoFooter(OpenCodeSnapshot openCode)
    {
        if (!openCode.HasGoSubscription)
            return "";

        var parts = new List<string>();
        if (openCode.GoRolling.ResetsAt is { } rollingReset)
            parts.Add($"5h resets {FormatResetTime(rollingReset)}");
        if (openCode.GoWeekly.ResetsAt is { } weeklyReset)
            parts.Add($"weekly resets {FormatResetTime(weeklyReset)}");
        if (openCode.GoMonthly.ResetsAt is { } monthlyReset)
            parts.Add($"monthly resets {FormatResetTime(monthlyReset)}");

        return string.Join(" · ", parts);
    }

    public static string FormatResetTimes(DateTimeOffset? sessionResetsAt, DateTimeOffset? weeklyResetsAt)
    {
        var parts = new List<string>();
        if (sessionResetsAt is { } sessionReset)
            parts.Add($"5h resets {FormatResetTime(sessionReset)}");

        if (weeklyResetsAt is { } weeklyReset)
            parts.Add($"weekly resets {FormatResetTime(weeklyReset)}");

        return string.Join(" · ", parts);
    }

    public static string FormatCodexFooter(CodexSnapshot codex)
    {
        if (!codex.IsAvailable)
            return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(codex.PlanLabel))
            parts.Add(codex.PlanLabel);

        if (codex.CreditsBalanceUsd is { } balance)
            parts.Add($"${balance.ToString("F0", CultureInfo.InvariantCulture)} credits");

        var resets = FormatResetTimes(codex.SessionResetsAt, codex.WeeklyResetsAt);
        if (!string.IsNullOrEmpty(resets))
            parts.Add(resets);

        return string.Join(" · ", parts);
    }

    public static string FormatClaudeProFooter(ClaudeProSnapshot pro)
    {
        if (!pro.IsAvailable)
            return "";

        return FormatResetTimes(pro.SessionResetsAt, pro.WeeklyResetsAt);
    }

    public static string FormatAntigravityFooter(AntigravitySnapshot snapshot)
    {
        if (!snapshot.IsAvailable)
            return "";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(snapshot.PlanLabel))
            parts.Add(snapshot.PlanLabel);

        var resets = FormatAntigravityResetTimes(snapshot);
        if (!string.IsNullOrEmpty(resets))
            parts.Add(resets);

        return string.Join(" · ", parts);
    }

    private static string FormatAntigravityResetTimes(AntigravitySnapshot snapshot)
    {
        var sessionResetsAt = FirstReset(snapshot.Gemini.SessionResetsAt, snapshot.ThirdParty.SessionResetsAt);
        var weeklyResetsAt = FirstReset(snapshot.Gemini.WeeklyResetsAt, snapshot.ThirdParty.WeeklyResetsAt);
        return FormatResetTimes(sessionResetsAt, weeklyResetsAt);
    }

    private static DateTimeOffset? FirstReset(DateTimeOffset? first, DateTimeOffset? second)
    {
        if (first is null)
            return second;
        if (second is null)
            return first;

        return first < second ? first : second;
    }

    private static string FormatResetTime(DateTimeOffset resetAt)
    {
        var local = resetAt.ToLocalTime();
        if (local.Date == DateTime.Today)
            return local.ToString("t", CultureInfo.CurrentCulture);

        if (local.Date < DateTime.Today.AddDays(7))
            return local.ToString("ddd h:mm tt", CultureInfo.CurrentCulture);

        return local.ToString("MMM d h:mm tt", CultureInfo.CurrentCulture);
    }

    public static void ApplyHeadline(
        double headlinePercent,
        bool isAvailable,
        string? statusMessage,
        TextBlock percentText,
        Grid mainTrack,
        Border mainFill,
        ref double lastHeadlinePercent)
    {
        if (!isAvailable)
        {
            percentText.Text = statusMessage ?? "—";
            percentText.Foreground = new SolidColorBrush(ErrorColor);
            mainFill.Width = 0;
            mainFill.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            mainTrack.Opacity = 0.45;
            lastHeadlinePercent = 0;
            return;
        }

        mainTrack.Opacity = 1;
        lastHeadlinePercent = headlinePercent;
        var rounded = Math.Round(headlinePercent);
        percentText.Text = $"{rounded.ToString(CultureInfo.InvariantCulture)}% used";
        var accent = UsageBarColors.GetColorForPercent(headlinePercent);
        percentText.Foreground = new SolidColorBrush(accent);
        mainFill.Background = new SolidColorBrush(accent);
        ProviderBarPresenter.UpdateProgressWidth(mainTrack, mainFill, headlinePercent);
    }

    public static void ApplyBreakdownSubBar(
        Grid track,
        Border fill,
        TextBlock percentText,
        ref double lastPercent,
        double percentUsed,
        bool isAvailable)
    {
        if (!isAvailable)
        {
            lastPercent = 0;
            fill.Width = 0;
            fill.Background = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));
            track.Opacity = 0.45;
            percentText.Text = "—";
            return;
        }

        track.Opacity = 1;
        lastPercent = percentUsed;
        var rounded = Math.Round(percentUsed);
        percentText.Text = $"{rounded.ToString(CultureInfo.InvariantCulture)}%";
        fill.Background = new SolidColorBrush(UsageBarColors.GetColorForPercent(percentUsed));
        ProviderBarPresenter.UpdateProgressWidth(track, fill, percentUsed);
    }

    public static void ApplyBreakdownLayout(
        bool showProBreakdown,
        bool isAvailable,
        bool isExpanded,
        string summaryText,
        string footerText,
        bool showFooter,
        TextBlock summary,
        StackPanel breakdownSection,
        Border breakdownPanel,
        TextBlock breakdownChevron,
        Border barBorder,
        TextBlock remainingText)
    {
        remainingText.Text = showFooter ? footerText : "";
        remainingText.IsVisible = showFooter && !string.IsNullOrEmpty(footerText);

        if (!showProBreakdown || !isAvailable)
        {
            breakdownSection.IsVisible = false;
            breakdownPanel.IsVisible = false;
            barBorder.Cursor = new Cursor(StandardCursorType.Arrow);
            return;
        }

        summary.Text = summaryText;
        breakdownSection.IsVisible = true;
        breakdownPanel.IsVisible = isExpanded;
        breakdownChevron.Text = isExpanded ? "\u25B4" : "\u25BE";
        barBorder.Cursor = new Cursor(StandardCursorType.Hand);
    }
}

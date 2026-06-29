using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CursorBreakdownPresenterTests
{
    [Fact]
    public void FormatSummary_rounds_auto_and_api_percents()
    {
        var summary = CursorBreakdownPresenter.FormatSummary(2.6, 0.4);

        Assert.Equal("3% Auto and 0% API used", summary);
    }

    [Fact]
    public void FormatApiPlanNote_includes_plan_limit_when_available()
    {
        var note = CursorBreakdownPresenter.FormatApiPlanNote(7000);

        Assert.Contains("$70", note);
        Assert.Contains("Your plan includes at least", note);
    }

    [Fact]
    public void FormatApiPlanNote_omits_plan_limit_when_missing()
    {
        var note = CursorBreakdownPresenter.FormatApiPlanNote(null);

        Assert.Equal("Additional usage beyond limits consumes on-demand spend.", note);
        Assert.DoesNotContain("Your plan includes", note);
    }
}

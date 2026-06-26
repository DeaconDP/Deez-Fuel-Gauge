using CursorUsageWidget.Models;
using Xunit;

namespace CursorUsageWidget.Tests;

public sealed class LimitSnapshotDetailLabelTests
{
    [Fact]
    public void CodexSnapshot_detail_label_uses_used_percent_format()
    {
        var observedAt = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var snapshot = CodexSnapshot.FromUsage("plus", 12, 34, observedAt.AddHours(2), observedAt.AddDays(5), null, false);

        Assert.Equal(88, snapshot.SessionPercentRemaining);
        Assert.Equal(66, snapshot.WeeklyPercentRemaining);
        Assert.Equal(12, snapshot.SessionPercentUsed);
        Assert.Equal(34, snapshot.WeeklyPercentUsed);
        Assert.Contains("5h 12%", snapshot.DetailLabel);
        Assert.Contains("wk 34%", snapshot.DetailLabel);
    }

    [Fact]
    public void AntigravityGroupSnapshot_detail_label_uses_used_percent_format()
    {
        var snapshot = AntigravityGroupSnapshot.FromUsage(80, 60, null, null);

        Assert.Equal(20, snapshot.SessionPercentUsed);
        Assert.Equal(40, snapshot.WeeklyPercentUsed);
        Assert.Equal("5h 20% · wk 40%", snapshot.DetailLabel);
    }
}

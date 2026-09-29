using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class LoginItemServiceTests
{
    [Fact]
    public void FormatWindowsRunValue_quotes_path()
    {
        Assert.Equal(
            "\"C:\\Apps\\DeezFuelGauge.exe\"",
            LoginItemService.FormatWindowsRunValue(@"C:\Apps\DeezFuelGauge.exe"));
    }

    [Fact]
    public void ParseWindowsRunValue_strips_quotes_and_whitespace()
    {
        Assert.Equal(
            @"C:\Apps\DeezFuelGauge.exe",
            LoginItemService.ParseWindowsRunValue("  \"C:\\Apps\\DeezFuelGauge.exe\"  "));
    }

    [Fact]
    public void NeedsWindowsPathRewrite_true_when_registered_path_missing()
    {
        var stale = LoginItemService.FormatWindowsRunValue(
            @"C:\Users\deaco\Documents\GitHub\cursor-usage-widget\DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe");
        var current = @"C:\Users\deaco\Documents\GitHub\Deez-Fuel-Gauge\DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe";

        var needsRewrite = LoginItemService.NeedsWindowsPathRewrite(
            stale,
            current,
            _ => false);

        Assert.True(needsRewrite);
    }

    [Fact]
    public void NeedsWindowsPathRewrite_true_when_path_moved()
    {
        var stale = LoginItemService.FormatWindowsRunValue(@"C:\Old\DeezFuelGauge.exe");
        var current = @"C:\New\DeezFuelGauge.exe";

        var needsRewrite = LoginItemService.NeedsWindowsPathRewrite(
            stale,
            current,
            path => path == current || path == @"C:\Old\DeezFuelGauge.exe");

        Assert.True(needsRewrite);
    }

    [Fact]
    public void NeedsWindowsPathRewrite_false_when_current_path_registered()
    {
        var current = @"C:\Users\deaco\Documents\GitHub\Deez-Fuel-Gauge\DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe";
        var registered = LoginItemService.FormatWindowsRunValue(current);

        var needsRewrite = LoginItemService.NeedsWindowsPathRewrite(
            registered,
            current,
            path => path == current);

        Assert.False(needsRewrite);
    }

    [Fact]
    public void NeedsWindowsPathRewrite_true_when_registration_empty()
    {
        Assert.True(LoginItemService.NeedsWindowsPathRewrite(
            null,
            @"C:\Apps\DeezFuelGauge.exe",
            _ => true));
    }
}

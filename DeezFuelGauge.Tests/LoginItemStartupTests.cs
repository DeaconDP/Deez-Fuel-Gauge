using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class LoginItemStartupTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constructing_the_window_never_contacts_the_system(bool launchAtLogin)
    {
        Assert.False(LoginItemStartup.ShouldContact(
            LoginItemMoment.Constructing,
            launchAtLogin,
            macOsSystemEvents: true));
    }

    [Fact]
    public void Mac_launch_with_login_off_does_not_contact_system_events()
    {
        Assert.False(LoginItemStartup.ShouldContact(
            LoginItemMoment.WindowShown,
            launchAtLogin: false,
            macOsSystemEvents: true));
    }

    [Fact]
    public void Mac_launch_with_login_on_contacts_after_the_window_is_shown()
    {
        Assert.True(LoginItemStartup.ShouldContact(
            LoginItemMoment.WindowShown,
            launchAtLogin: true,
            macOsSystemEvents: true));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Windows_launch_still_syncs_the_run_key_after_the_window_is_shown(bool launchAtLogin)
    {
        Assert.True(LoginItemStartup.ShouldContact(
            LoginItemMoment.WindowShown,
            launchAtLogin,
            macOsSystemEvents: false));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Changing_the_setting_contacts_on_either_platform(bool macOsSystemEvents)
    {
        var calls = new List<bool>();
        LoginItemStartup.ContactIfNeeded(
            LoginItemMoment.UserChanged,
            launchAtLogin: false,
            macOsSystemEvents,
            calls.Add);

        Assert.Equal([false], calls);
    }
}

namespace DeezFuelGauge.Services;

public enum LoginItemMoment
{
    Constructing,
    WindowShown,
    UserChanged
}

public static class LoginItemStartup
{
    public static bool ShouldContact(LoginItemMoment moment, bool launchAtLogin, bool macOsSystemEvents) =>
        true;

    public static void ContactIfNeeded(
        LoginItemMoment moment,
        bool launchAtLogin,
        bool macOsSystemEvents,
        Action<bool> contact)
    {
        if (ShouldContact(moment, launchAtLogin, macOsSystemEvents))
            contact(launchAtLogin);
    }
}

namespace DeezFuelGauge.Services;

public enum LoginItemMoment
{
    Constructing,
    WindowShown,
    UserChanged
}

public static class LoginItemStartup
{
    public static bool ShouldContact(LoginItemMoment moment, bool launchAtLogin, bool macOsSystemEvents)
    {
        if (moment == LoginItemMoment.Constructing)
            return false;

        if (moment == LoginItemMoment.UserChanged)
            return true;

        if (macOsSystemEvents)
            return launchAtLogin;

        return true;
    }

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

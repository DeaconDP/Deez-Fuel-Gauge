namespace CursorUsageWidget.Services;

public static class PlatformPaths
{
    public static string CursorStateDatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor", "User", "globalStorage", "state.vscdb");

    public static IReadOnlyList<string> AntigravityStateDatabasePaths
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return
            [
                Path.Combine(appData, "Antigravity IDE", "User", "globalStorage", "state.vscdb"),
                Path.Combine(appData, "Antigravity", "User", "globalStorage", "state.vscdb")
            ];
        }
    }

    public static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "cursor-usage-widget");

    public static string ClaudeConfigDirectory
    {
        get
        {
            var configDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR");
            if (!string.IsNullOrWhiteSpace(configDir))
                return configDir;

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        }
    }

    public static string ClaudeCodeCredentialsPath =>
        Path.Combine(ClaudeConfigDirectory, ".credentials.json");
}

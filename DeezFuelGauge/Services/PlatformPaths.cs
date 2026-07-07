using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services;

public static class PlatformPaths
{
    public static IReadOnlyList<string> CursorExecutablePaths
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ["/Applications/Cursor.app"];
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return
            [
                Path.Combine(localAppData, "Programs", "cursor", "Cursor.exe"),
                Path.Combine(localAppData, "cursor", "Cursor.exe")
            ];
        }
    }

    public static IReadOnlyList<string> AntigravityIdeExecutablePaths
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return ["/Applications/Antigravity IDE.app"];
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return
            [
                Path.Combine(localAppData, "Programs", "Antigravity IDE", "Antigravity IDE.exe"),
                Path.Combine(localAppData, "Antigravity IDE", "Antigravity IDE.exe")
            ];
        }
    }

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
            AppBranding.SettingsSlug);

    public static string LegacySettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppBranding.LegacySettingsSlug);

    public static string GeminiConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gemini");

    public static string GeminiOAuthCredentialsPath =>
        Path.Combine(GeminiConfigDirectory, "oauth_creds.json");

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

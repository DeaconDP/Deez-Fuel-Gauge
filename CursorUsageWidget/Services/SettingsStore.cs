using System.IO;
using System.Text.Json;
using CursorUsageWidget.Models;

namespace CursorUsageWidget.Services;

public static class SettingsStore
{
    private static readonly string SettingsDir = PlatformPaths.SettingsDirectory;

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static WidgetSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new WidgetSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<WidgetSettings>(json) ?? new WidgetSettings();
            MigrateGeminiSettings(settings);
            return settings;
        }
        catch
        {
            return new WidgetSettings();
        }
    }

    internal static void MigrateGeminiSettings(WidgetSettings settings)
    {
        if (!settings.Gemini.ShowDirectSource)
            return;

        settings.Gemini.ShowProLimits = true;
        settings.Gemini.ShowDirectSource = false;
    }

    public static void Save(WidgetSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // ignore persistence errors
        }
    }
}

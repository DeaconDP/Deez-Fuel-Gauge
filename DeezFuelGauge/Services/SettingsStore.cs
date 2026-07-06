using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public static class SettingsStore
{
    private static readonly string SettingsDir = PlatformPaths.SettingsDirectory;

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    public static WidgetSettings Load()
    {
        SettingsMigration.MigrateLegacyDataIfNeeded();

        try
        {
            if (!File.Exists(SettingsPath))
                return CreateDefaultSettings();

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<WidgetSettings>(json) ?? CreateDefaultSettings();
            MigrateGeminiSettings(settings);
            MigrateDiskSettings(settings, json);
            MigrateSystemSettings(settings, json);
            return settings;
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    private static WidgetSettings CreateDefaultSettings() => new()
    {
        DiskAggregateVolumes = DiskSpaceProvider.DefaultDiskAggregateVolumes()
    };

    internal static void MigrateGeminiSettings(WidgetSettings settings)
    {
        if (!settings.Gemini.ShowDirectSource)
            return;

        settings.Gemini.ShowProLimits = true;
        settings.Gemini.ShowDirectSource = false;
    }

    internal static void MigrateDiskSettings(WidgetSettings settings, string? rawJson)
    {
        settings.DisabledDiskDrives ??= [];

        if (!string.IsNullOrEmpty(rawJson) && rawJson.Contains("\"DiskAggregateVolumes\"", StringComparison.Ordinal))
            return;

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return;

        settings.DiskAggregateVolumes = true;

        var drives = DiskSpaceProvider.GetReadyDrives();
        var primaryName = DiskSpaceProvider.SelectMacOsPrimaryDriveName(drives.Select(d => d.Name));
        if (primaryName is null)
            return;

        settings.DisabledDiskDrives = drives
            .Where(d => !string.Equals(d.Name, primaryName, StringComparison.Ordinal))
            .Select(d => d.Name)
            .ToList();
    }

    internal static void MigrateSystemSettings(WidgetSettings settings, string? rawJson)
    {
        if (!string.IsNullOrEmpty(rawJson) && rawJson.Contains("\"ShowSystemResources\"", StringComparison.Ordinal))
            return;

        settings.ShowSystemResources = true;
        settings.ShowSystemDetails = true;
        settings.ShowRam = true;
        settings.ShowCpu = true;
        settings.ShowGpu = true;
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

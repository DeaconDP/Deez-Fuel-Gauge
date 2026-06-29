namespace DeezFuelGauge.Services;

public static class SettingsMigration
{
    internal static void MigrateLegacyDataIfNeeded(string newDir, string legacyDir)
    {
        if (Directory.Exists(newDir) && Directory.EnumerateFileSystemEntries(newDir).Any())
            return;

        if (!Directory.Exists(legacyDir))
            return;

        Directory.CreateDirectory(newDir);
        CopyDirectory(legacyDir, newDir);
    }

    public static void MigrateLegacyDataIfNeeded()
    {
        MigrateLegacyDataIfNeeded(PlatformPaths.SettingsDirectory, PlatformPaths.LegacySettingsDirectory);
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destinationDir, Path.GetRelativePath(sourceDir, directory));
            Directory.CreateDirectory(target);
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destinationDir, Path.GetRelativePath(sourceDir, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}

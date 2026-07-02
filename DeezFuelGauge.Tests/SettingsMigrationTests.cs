using System.Text;
using DeezFuelGauge.Services;
using DeezFuelGauge.Services.Credentials;
using Xunit;

namespace DeezFuelGauge.Tests;

public class SettingsMigrationTests
{
    [Fact]
    public void MigrateLegacyDataIfNeeded_copies_legacy_settings_when_new_directory_is_empty()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var legacyDir = Path.Combine(root, "legacy");
        var newDir = Path.Combine(root, "new");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(Path.Combine(legacyDir, "credentials"));
        File.WriteAllText(Path.Combine(legacyDir, "settings.json"), """{"RefreshIntervalMinutes":15}""");
        File.WriteAllText(Path.Combine(legacyDir, "credentials", "openai-test.bin"), "secret");

        try
        {
            SettingsMigration.MigrateLegacyDataIfNeeded(newDir, legacyDir);

            Assert.True(File.Exists(Path.Combine(newDir, "settings.json")));
            Assert.True(File.Exists(Path.Combine(newDir, "credentials", "openai-test.bin")));
            Assert.Equal("""{"RefreshIntervalMinutes":15}""", File.ReadAllText(Path.Combine(newDir, "settings.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_skips_when_new_directory_already_has_data()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var legacyDir = Path.Combine(root, "legacy");
        var newDir = Path.Combine(root, "new");
        Directory.CreateDirectory(legacyDir);
        Directory.CreateDirectory(newDir);
        File.WriteAllText(Path.Combine(legacyDir, "settings.json"), """{"RefreshIntervalMinutes":5}""");
        File.WriteAllText(Path.Combine(newDir, "settings.json"), """{"RefreshIntervalMinutes":30}""");

        try
        {
            SettingsMigration.MigrateLegacyDataIfNeeded(newDir, legacyDir);

            Assert.Equal("""{"RefreshIntervalMinutes":30}""", File.ReadAllText(Path.Combine(newDir, "settings.json")));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}

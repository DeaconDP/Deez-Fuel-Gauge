using CursorUsageWidget.Services;
using Xunit;

namespace CursorUsageWidget.Tests;

public class PlatformPathsTests
{
    [Fact]
    public void CursorStateDatabasePath_uses_cursor_global_storage()
    {
        var path = PlatformPaths.CursorStateDatabasePath;

        Assert.EndsWith(
            Path.Combine("Cursor", "User", "globalStorage", "state.vscdb"),
            path);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            path);
    }

    [Fact]
    public void SettingsDirectory_uses_local_app_data()
    {
        var path = PlatformPaths.SettingsDirectory;

        Assert.EndsWith("cursor-usage-widget", path);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            path);
    }

    [Fact]
    public void CursorTokenReader_database_path_matches_platform_paths()
    {
        Assert.Equal(PlatformPaths.CursorStateDatabasePath, CursorTokenReader.DatabasePath);
    }
}

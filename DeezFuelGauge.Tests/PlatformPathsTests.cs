using DeezFuelGauge.Services;
using Xunit;

namespace DeezFuelGauge.Tests;

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

        Assert.EndsWith("deez-fuel-gauge", path);
        Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            path);
    }

    [Fact]
    public void AntigravityStateDatabasePaths_include_antigravity_global_storage()
    {
        var paths = PlatformPaths.AntigravityStateDatabasePaths;

        Assert.Equal(2, paths.Count);
        Assert.Contains(paths, path => path.EndsWith(Path.Combine("Antigravity IDE", "User", "globalStorage", "state.vscdb")));
        Assert.Contains(paths, path => path.EndsWith(Path.Combine("Antigravity", "User", "globalStorage", "state.vscdb")));
        Assert.All(paths, path => Assert.StartsWith(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            path));
    }
}

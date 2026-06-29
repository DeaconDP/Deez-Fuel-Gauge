using CursorUsageWidget.Setup;
using Xunit;

namespace CursorUsageWidget.Tests;

public class MacOsPackagingTests
{
    [Fact]
    public void MacOs_packaging_files_exist()
    {
        var repoRoot = FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(repoRoot, "packaging", "macos", "Info.plist")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "packaging", "icons", "app-icon.ico")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "packaging", "icons", "app-icon.png")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "packaging", "icons", "AppIcon.icns")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "generate-app-icons.py")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "package-macos-app.sh")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "ensure-dotnet8-sdk.sh")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "package-macos-release.sh")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "scripts", "refresh-macos-setup-launcher.sh")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "Releases", "assets.macos.json")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "CursorUsageWidget.Setup", "CursorUsageWidget.Setup.csproj")));
        Assert.True(Directory.Exists(Path.Combine(repoRoot, "setup-and-run.app", "Contents", "MacOS")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "setup-and-run.app", "Contents", "MacOS", "setup-and-run")));
        Assert.True(File.Exists(Path.Combine(repoRoot, "setup-and-run.app", "Contents", "PkgInfo")));
    }

    [Fact]
    public void MacOs_widget_info_plist_declares_app_host()
    {
        var plistPath = Path.Combine(FindRepoRoot(), "packaging", "macos", "Info.plist");
        var plist = File.ReadAllText(plistPath);

        Assert.Contains("<key>CFBundleExecutable</key>", plist);
        Assert.Contains("<string>CursorUsageWidget</string>", plist);
        Assert.Contains("<key>CFBundleIdentifier</key>", plist);
        Assert.Contains("<key>CFBundleIconFile</key>", plist);
        Assert.Contains("<string>AppIcon</string>", plist);
    }

    [Fact]
    public void Package_script_resolves_repo_root_from_scripts_directory()
    {
        var script = File.ReadAllText(Path.Combine(FindRepoRoot(), "scripts", "package-macos-app.sh"));

        Assert.Contains("REPO_ROOT=\"$(cd \"$(dirname \"$0\")/..\" && pwd)\"", script);
        Assert.DoesNotContain("$(dirname \"$0\")/../..", script);
    }

    [Fact]
    public void Setup_app_info_plist_declares_icon()
    {
        var plistPath = Path.Combine(FindRepoRoot(), "setup-and-run.app", "Contents", "Info.plist");
        var plist = File.ReadAllText(plistPath);

        Assert.Contains("<key>CFBundleIconFile</key>", plist);
        Assert.Contains("<string>AppIcon</string>", plist);
    }

    [Fact]
    public void Windows_release_script_exists()
    {
        Assert.True(File.Exists(Path.Combine(FindRepoRoot(), "scripts", "package-windows-release.ps1")));
    }

    [Fact]
    public void CopyPublishedFiles_copies_native_libraries_from_publish_output()
    {
        var publishDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var macOsDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        try
        {
            Directory.CreateDirectory(publishDir);
            Directory.CreateDirectory(macOsDir);

            File.WriteAllText(Path.Combine(publishDir, "CursorUsageWidget"), "host");
            File.WriteAllText(Path.Combine(publishDir, "CursorUsageWidget.pdb"), "debug");
            foreach (var library in MacOsAppPackager.RequiredNativeLibraries)
                File.WriteAllText(Path.Combine(publishDir, library), library);

            MacOsAppPackager.CopyPublishedFiles(publishDir, macOsDir);

            Assert.True(File.Exists(Path.Combine(macOsDir, "CursorUsageWidget")));
            Assert.False(File.Exists(Path.Combine(macOsDir, "CursorUsageWidget.pdb")));
            foreach (var library in MacOsAppPackager.RequiredNativeLibraries)
                Assert.True(File.Exists(Path.Combine(macOsDir, library)));
        }
        finally
        {
            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, recursive: true);
            if (Directory.Exists(macOsDir))
                Directory.Delete(macOsDir, recursive: true);
        }
    }

    [Fact]
    public void HasDotNet8Sdk_matches_dotnet_list_sdks_output()
    {
        var dotnet = MacOsAppPackager.FindDotnet();
        if (dotnet is null)
            return;

        string listedSdks;
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = dotnet,
                Arguments = "--list-sdks",
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            using var process = System.Diagnostics.Process.Start(startInfo)
                ?? throw new InvalidOperationException("Failed to start dotnet.");
            listedSdks = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
        }
        catch
        {
            return;
        }

        var expected = listedSdks.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => line.StartsWith("8.", StringComparison.Ordinal));
        Assert.Equal(expected, MacOsAppPackager.HasDotNet8Sdk(dotnet));
    }

    [Fact]
    public void FindRepoRoot_locates_solution_from_test_output_directory()
    {
        var repoRoot = MacOsAppPackager.FindRepoRoot();
        Assert.True(File.Exists(Path.Combine(repoRoot, "CursorUsageWidget.sln")));
        Assert.True(Directory.Exists(Path.Combine(repoRoot, "setup-and-run.app")));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CursorUsageWidget.sln")))
                return directory.FullName;

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}

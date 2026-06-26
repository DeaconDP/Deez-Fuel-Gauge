using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorUsageWidget.Setup;

public static class MacOsAppPackager
{
    public static string Package(string repoRoot, string dotnetPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            throw new PlatformNotSupportedException("macOS app packaging is only supported on macOS.");

        var rid = RuntimeInformation.ProcessArchitecture switch
        {
            Architecture.Arm64 => "osx-arm64",
            Architecture.X64 => "osx-x64",
            _ => throw new PlatformNotSupportedException(
                $"Unsupported macOS architecture: {RuntimeInformation.ProcessArchitecture}")
        };

        var project = Path.Combine(repoRoot, "CursorUsageWidget", "CursorUsageWidget.csproj");
        var publishDir = Path.Combine(repoRoot, "CursorUsageWidget", "bin", "Release", "net8.0", rid, "publish");
        var appPath = Path.Combine(repoRoot, "CursorUsageWidget.app");
        var infoPlist = Path.Combine(repoRoot, "packaging", "macos", "Info.plist");
        var appHostPath = Path.Combine(publishDir, "CursorUsageWidget");

        RunProcess(dotnetPath, new[]
        {
            "publish", project,
            "-c", "Release",
            "-r", rid,
            "--self-contained", "true",
            "-p:UseAppHost=true",
            "-p:PublishSingleFile=false",
            "--nologo"
        });

        if (!File.Exists(appHostPath))
            throw new FileNotFoundException("Published app host was not created.", appHostPath);

        var macOsDir = Path.Combine(appPath, "Contents", "MacOS");
        var resourcesDir = Path.Combine(appPath, "Contents", "Resources");

        if (Directory.Exists(appPath))
            Directory.Delete(appPath, recursive: true);

        Directory.CreateDirectory(macOsDir);
        Directory.CreateDirectory(resourcesDir);

        File.Copy(infoPlist, Path.Combine(appPath, "Contents", "Info.plist"), overwrite: true);
        File.WriteAllText(Path.Combine(appPath, "Contents", "PkgInfo"), "APPL????");

        CopyDirectory(publishDir, macOsDir);
        MakeExecutable(appHostPath);
        SignAppBundle(appPath);
        SignAppBundle(Path.Combine(repoRoot, "setup-and-run.app"));

        return appPath;
    }

    public static void OpenApp(string appPath)
    {
        RunProcess("/usr/bin/open", new[] { appPath });
    }

    public static string? FindDotnet()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var extraPaths = new[]
        {
            "/usr/local/share/dotnet",
            "/opt/homebrew/bin",
            "/usr/local/bin",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "tools")
        };

        foreach (var directory in extraPaths.Concat(path.Split(':', StringSplitOptions.RemoveEmptyEntries)))
        {
            var candidate = Path.Combine(directory.Trim(), "dotnet");
            if (File.Exists(candidate))
                return candidate;
        }

        var knownCandidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dotnet", "dotnet"),
            "/usr/local/share/dotnet/dotnet",
            "/opt/homebrew/bin/dotnet",
            "/usr/local/bin/dotnet"
        };

        return knownCandidates.FirstOrDefault(File.Exists);
    }

    public static string FindRepoRoot()
    {
        var setupBundle = FindSetupBundleDirectory();
        if (setupBundle is not null)
        {
            var parent = Directory.GetParent(setupBundle);
            if (parent is not null)
                return parent.FullName;
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "CursorUsageWidget.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string? FindSetupBundleDirectory()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (string.Equals(current.Name, "setup-and-run.app", StringComparison.Ordinal))
                return current.FullName;

            current = current.Parent;
        }

        return null;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(directory.Replace(sourceDir, destinationDir, StringComparison.Ordinal));

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var destination = file.Replace(sourceDir, destinationDir, StringComparison.Ordinal);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void MakeExecutable(string path)
    {
        if (!File.Exists(path))
            return;

        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(path, mode);
    }

    private static void SignAppBundle(string appPath)
    {
        if (!Directory.Exists(appPath))
            return;

        RunProcessOptional("/usr/bin/codesign", new[] { "--force", "--deep", "--sign", "-", appPath });
        RunProcessOptional("/usr/bin/xattr", new[] { "-cr", appPath });
    }

    private static void RunProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"{fileName} exited with code {process.ExitCode}.{Environment.NewLine}{stderr}{Environment.NewLine}{stdout}");
    }

    private static void RunProcessOptional(string fileName, IReadOnlyList<string> arguments)
    {
        if (!File.Exists(fileName))
            return;

        try
        {
            RunProcess(fileName, arguments);
        }
        catch
        {
            // Ad-hoc signing is best-effort for local installs.
        }
    }
}

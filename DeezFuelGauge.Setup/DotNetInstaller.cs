using System.Diagnostics;

namespace DeezFuelGauge.Setup;

public static class DotNetInstaller
{
    private const string InstallScriptUrl =
        "https://dot.net/v1/dotnet-install.sh";

    public static string? TryInstallSdk(string logPath)
    {
        var installDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".dotnet");

        try
        {
            Directory.CreateDirectory(installDir);
            var scriptPath = Path.Combine(Path.GetTempPath(), $"dotnet-install-{Guid.NewGuid():N}.sh");
            DownloadInstallScript(scriptPath, logPath);
            RunInstallScript(scriptPath, installDir, logPath);

            var dotnetPath = Path.Combine(installDir, "dotnet");
            return File.Exists(dotnetPath) ? dotnetPath : null;
        }
        catch (Exception ex)
        {
            AppendLog(logPath, $"dotnet-install failed: {ex}");
            return null;
        }
    }

    private static void DownloadInstallScript(string scriptPath, string logPath)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        var script = client.GetStringAsync(InstallScriptUrl).GetAwaiter().GetResult();
        File.WriteAllText(scriptPath, script);
        MakeExecutable(scriptPath);
        AppendLog(logPath, $"Downloaded dotnet-install.sh to {scriptPath}");
    }

    private static void RunInstallScript(string scriptPath, string installDir, string logPath)
    {
        var startInfo = new ProcessStartInfo("/bin/bash")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--channel");
        startInfo.ArgumentList.Add("8.0");
        startInfo.ArgumentList.Add("--install-dir");
        startInfo.ArgumentList.Add(installDir);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start dotnet-install.sh");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        AppendLog(logPath, stdout);
        if (!string.IsNullOrWhiteSpace(stderr))
            AppendLog(logPath, stderr);

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"dotnet-install.sh exited with code {process.ExitCode}");
    }

    private static void MakeExecutable(string path)
    {
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
        File.SetUnixFileMode(path, mode);
    }

    private static void AppendLog(string logPath, string message)
    {
        try
        {
            File.AppendAllText(logPath, message + Environment.NewLine);
        }
        catch
        {
            // Best-effort logging.
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services;

public static class LoginItemService
{
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = AppBranding.AssemblyName;
    private const string LegacyAppName = AppBranding.LegacyAssemblyName;

    public static bool IsSupported =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        || RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

    public static bool IsEnabled()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return IsWindowsLoginItemEnabled();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return IsMacOsLoginItemEnabled();

        return false;
    }

    /// <summary>
    /// Converges the OS login item to <paramref name="enabled"/> using this process's
    /// executable path. Re-runs are safe and heal stale paths after moves or renames.
    /// </summary>
    public static void SyncDesiredState(bool enabled)
    {
        if (!IsSupported)
            return;

        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return;

        if (enabled && !File.Exists(exe))
            return;

        SetEnabled(enabled, exe);
    }

    public static void SetEnabled(bool enabled, string executablePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SetWindowsLoginItem(enabled, executablePath);
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            SetMacOsLoginItem(enabled, executablePath);
    }

    internal static string FormatWindowsRunValue(string executablePath) =>
        $"\"{executablePath}\"";

    internal static string? ParseWindowsRunValue(string? registeredValue)
    {
        if (string.IsNullOrWhiteSpace(registeredValue))
            return null;

        return registeredValue.Trim().Trim('"');
    }

    internal static bool NeedsWindowsPathRewrite(
        string? registeredValue,
        string currentExecutablePath,
        Func<string, bool>? fileExists = null)
    {
        fileExists ??= File.Exists;
        var registeredPath = ParseWindowsRunValue(registeredValue);
        if (registeredPath is null)
            return true;
        if (!fileExists(registeredPath))
            return true;

        return !string.Equals(registeredPath, currentExecutablePath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWindowsLoginItemEnabled()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WindowsRunKey, writable: false);
        var current = key?.GetValue(AppName) as string;
        if (!string.IsNullOrWhiteSpace(current))
            return true;

        var legacy = key?.GetValue(LegacyAppName) as string;
        return !string.IsNullOrWhiteSpace(legacy);
    }

    private static void SetWindowsLoginItem(bool enabled, string executablePath)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(WindowsRunKey, writable: true)
                        ?? Microsoft.Win32.Registry.CurrentUser.CreateSubKey(WindowsRunKey);

        if (key is null)
            return;

        if (enabled)
        {
            key.DeleteValue(LegacyAppName, throwOnMissingValue: false);
            key.SetValue(AppName, FormatWindowsRunValue(executablePath));
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
            key.DeleteValue(LegacyAppName, throwOnMissingValue: false);
        }
    }

    private static bool IsMacOsLoginItemEnabled()
    {
        var appPath = ResolveMacOsAppBundlePath();
        if (appPath is null)
            return false;

        var script =
            $"tell application \"System Events\" to get the name of every login item";
        var output = RunProcess("/usr/bin/osascript", ["-e", script]);
        return output.Contains(Path.GetFileNameWithoutExtension(appPath), StringComparison.OrdinalIgnoreCase);
    }

    private static void SetMacOsLoginItem(bool enabled, string executablePath)
    {
        var appPath = ResolveMacOsAppBundlePath() ?? executablePath;
        if (enabled)
        {
            RemoveMacOsLoginItem(AppBranding.LegacyAssemblyName);
            // Always re-add so a moved/rebuilt .app path replaces a stale login item.
            RemoveMacOsLoginItem(Path.GetFileNameWithoutExtension(appPath));
            RunProcess("/usr/bin/osascript", [
                "-e",
                $"tell application \"System Events\" to make login item at end with properties {{path:\"{appPath}\", hidden:false}}"]);
        }
        else
        {
            RunProcess("/usr/bin/osascript", [
                "-e",
                $"tell application \"System Events\" to delete login item \"{Path.GetFileNameWithoutExtension(appPath)}\""]);
            RemoveMacOsLoginItem(AppBranding.LegacyAssemblyName);
        }
    }

    private static void RemoveMacOsLoginItem(string loginItemName)
    {
        RunProcess("/usr/bin/osascript", [
            "-e",
            $"tell application \"System Events\" to delete login item \"{loginItemName}\""]);
    }

    private static string? ResolveMacOsAppBundlePath()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
            return null;

        var dir = new DirectoryInfo(Path.GetDirectoryName(exe)!);
        while (dir is not null)
        {
            if (dir.Name.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
                return dir.FullName;
            dir = dir.Parent;
        }

        return null;
    }

    private static string RunProcess(string fileName, string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in args)
            startInfo.ArgumentList.Add(arg);

        using var process = Process.Start(startInfo);

        if (process is null)
            return "";

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}

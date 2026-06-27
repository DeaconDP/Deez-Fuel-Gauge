using System.Diagnostics;
using System.Runtime.InteropServices;
using CursorUsageWidget.Setup;

if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    Console.Error.WriteLine("Cursor Usage Widget setup is only supported on macOS.");
    return 1;
}

var logDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "Library", "Logs", "CursorUsageWidget");
Directory.CreateDirectory(logDir);
var logPath = Path.Combine(logDir, "setup.log");

try
{
    File.AppendAllText(logPath, $"{Environment.NewLine}=== setup {DateTimeOffset.Now:u} ==={Environment.NewLine}");

    var repoRoot = MacOsAppPackager.FindRepoRoot();
    var dotnet = MacOsAppPackager.FindDotnet();
    if (dotnet is null)
    {
        File.AppendAllText(logPath, $"dotnet not found; attempting dotnet-install.sh...{Environment.NewLine}");
        dotnet = DotNetInstaller.TryInstallSdk(logPath);
    }

    if (dotnet is null)
    {
        ShowMessage(
            "The .NET 8 SDK is required. We opened the download page in your browser. Install it, then double-click setup-and-run again.");
        Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/8.0")
        {
            UseShellExecute = true
        });
        return 1;
    }

    var appPath = MacOsAppPackager.Package(repoRoot, dotnet);
    MacOsAppPackager.OpenApp(appPath);
    File.AppendAllText(logPath, $"Opened {appPath}{Environment.NewLine}");
    return 0;
}
catch (Exception ex)
{
    File.AppendAllText(logPath, ex + Environment.NewLine);
    ShowMessage(
        $"Setup failed: {ex.Message} Details were saved to {logPath}.");
    return 1;
}

static void ShowMessage(string message)
{
    try
    {
        Process.Start(new ProcessStartInfo("/usr/bin/osascript")
        {
            ArgumentList =
            {
                "-e",
                $"display dialog {Quote(message)} buttons {{\"OK\"}} default button \"OK\" with title \"Cursor Usage Widget\""
            },
            UseShellExecute = false
        })?.WaitForExit();
    }
    catch
    {
        // Best-effort UI when launched without a desktop session.
    }
}

static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

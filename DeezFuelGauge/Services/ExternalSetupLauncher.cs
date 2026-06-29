using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DeezFuelGauge.Services;

public class ExternalSetupLauncher
{
    public virtual void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public virtual void LaunchCodexLogin()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo("cmd.exe")
            {
                Arguments = "/c start \"Codex Login\" cmd /k codex login",
                UseShellExecute = false
            });
            return;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Process.Start(new ProcessStartInfo("/usr/bin/osascript")
            {
                ArgumentList = { "-e", "tell application \"Terminal\" to do script \"codex login\"" },
                UseShellExecute = false
            });
            return;
        }

        Process.Start(new ProcessStartInfo("codex")
        {
            Arguments = "login",
            UseShellExecute = true
        });
    }

    public void OpenChatGpt() => OpenUrl("https://chatgpt.com");

    public void OpenClaudeAi() => OpenUrl("https://claude.ai/settings/usage");

    public void OpenAntigravity() => OpenUrl("https://antigravity.google/");

    public void OpenOpenCode() => OpenUrl("https://opencode.ai");

    public void OpenOpenRouter() => OpenUrl("https://openrouter.ai/settings/keys");
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CursorUsageWidget.Services;

public static class ClaudeCodeTokenReader
{
    private const string KeychainServicePrefix = "Claude Code-credentials";

    public static ClaudeCodeOAuthCredential? Read()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var fromKeychain = ReadFromMacKeychain();
            if (fromKeychain is not null)
                return fromKeychain;
        }

        return ReadFromCredentialsFile(PlatformPaths.ClaudeCodeCredentialsPath);
    }

    internal static ClaudeCodeOAuthCredential? ParseCredentialsJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseCredentialsRoot(document.RootElement);
        }
        catch
        {
            return null;
        }
    }

    internal static ClaudeCodeOAuthCredential? ParseCredentialsRoot(JsonElement root)
    {
        if (!root.TryGetProperty("claudeAiOauth", out var oauth) || oauth.ValueKind != JsonValueKind.Object)
            return null;

        if (!oauth.TryGetProperty("accessToken", out var accessTokenEl)
            || accessTokenEl.ValueKind != JsonValueKind.String)
            return null;

        var accessToken = accessTokenEl.GetString();
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        long? expiresAt = null;
        if (oauth.TryGetProperty("expiresAt", out var expiresAtEl) && expiresAtEl.ValueKind == JsonValueKind.Number)
            expiresAt = expiresAtEl.GetInt64();

        return new ClaudeCodeOAuthCredential
        {
            AccessToken = accessToken,
            ExpiresAt = expiresAt
        };
    }

    internal static string BuildKeychainServiceName(string configDir)
    {
        var absolute = Path.GetFullPath(configDir);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(absolute));
        var prefix = Convert.ToHexString(hash)[..8].ToLowerInvariant();
        return $"{KeychainServicePrefix}-{prefix}";
    }

    private static ClaudeCodeOAuthCredential? ReadFromCredentialsFile(string path)
    {
        if (!File.Exists(path))
            return null;

        try
        {
            var json = File.ReadAllText(path);
            return ParseCredentialsJson(json);
        }
        catch
        {
            return null;
        }
    }

    private static ClaudeCodeOAuthCredential? ReadFromMacKeychain()
    {
        try
        {
            var service = BuildKeychainServiceName(PlatformPaths.ClaudeConfigDirectory);
            var startInfo = new ProcessStartInfo("/usr/bin/security", $"find-generic-password -s \"{service}\" -w")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process is null)
                return null;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return null;

            return ParseCredentialsJson(output.Trim());
        }
        catch
        {
            return null;
        }
    }
}

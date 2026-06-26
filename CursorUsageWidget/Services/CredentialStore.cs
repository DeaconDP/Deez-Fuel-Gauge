using System.IO;
using System.Text;
using CursorUsageWidget.Services.Credentials;

namespace CursorUsageWidget.Services;

public static class CredentialStore
{
    private static readonly string CredentialsDir = Path.Combine(PlatformPaths.SettingsDirectory, "credentials");

    public static string Store(string provider, string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new ArgumentException("Secret cannot be empty.", nameof(secret));

        Directory.CreateDirectory(CredentialsDir);
        var id = $"{provider}-{Guid.NewGuid():N}";
        var path = GetPath(id);
        var protectedBytes = CredentialProtector.Protect(Encoding.UTF8.GetBytes(secret));
        File.WriteAllBytes(path, protectedBytes);
        return id;
    }

    public static string? Retrieve(string? credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            return null;

        var path = GetPath(credentialId);
        if (!File.Exists(path))
            return null;

        try
        {
            var protectedBytes = File.ReadAllBytes(path);
            var plainBytes = CredentialProtector.Unprotect(protectedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }

    public static void Delete(string? credentialId)
    {
        if (string.IsNullOrWhiteSpace(credentialId))
            return;

        var path = GetPath(credentialId);
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // ignore
        }
    }

    public static void Replace(string provider, string? existingId, string newSecret, Action<string?> setCredentialId)
    {
        if (string.IsNullOrWhiteSpace(newSecret))
        {
            Delete(existingId);
            setCredentialId(null);
            return;
        }

        if (!string.IsNullOrWhiteSpace(existingId))
        {
            Delete(existingId);
        }

        setCredentialId(Store(provider, newSecret));
    }

    private static string GetPath(string credentialId)
    {
        var safeId = credentialId.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');
        return Path.Combine(CredentialsDir, $"{safeId}.cred");
    }
}

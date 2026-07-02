using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorUsageWidget.Services;

public sealed class ChromiumCookieReadResult
{
    public string? Value { get; init; }
    public IReadOnlyList<string> LockedBrowsers { get; init; } = [];
    public bool HasUnreadableEncryptedCookie { get; init; }

    public bool IsSuccess => !string.IsNullOrWhiteSpace(Value);
}

public static class ChromiumCookieHelper
{
    public static string? TryReadCookie(
        string cookieName,
        IReadOnlyList<string> hostPatterns,
        IReadOnlyList<ChromiumBrowserProfile> profiles,
        Func<byte[]?, byte[], string?>? decryptValue = null) =>
        TryReadCookies([cookieName], hostPatterns, profiles, decryptValue).Value;

    public static ChromiumCookieReadResult TryReadCookies(
        IReadOnlyList<string> cookieNames,
        IReadOnlyList<string> hostPatterns,
        IReadOnlyList<ChromiumBrowserProfile> profiles,
        Func<byte[]?, byte[], string?>? decryptValue = null)
    {
        var decrypt = decryptValue ?? DecryptChromiumCookieValue;
        var lockedBrowsers = new List<string>();
        var hasUnreadableEncryptedCookie = false;

        foreach (var profile in profiles)
        {
            if (!File.Exists(profile.CookieDatabasePath))
                continue;

            var tempCopy = Path.Combine(Path.GetTempPath(), $"chromium-cookies-{Guid.NewGuid():N}.db");
            byte[]? encryptionKey;
            try
            {
                File.Copy(profile.CookieDatabasePath, tempCopy, overwrite: true);
                encryptionKey = ReadChromiumEncryptionKey(profile.LocalStatePath, profile.KeychainLabel);
            }
            catch (IOException)
            {
                if (!string.IsNullOrWhiteSpace(profile.KeychainLabel))
                    lockedBrowsers.Add(profile.KeychainLabel);
                continue;
            }
            catch (SqliteException)
            {
                if (!string.IsNullOrWhiteSpace(profile.KeychainLabel))
                    lockedBrowsers.Add(profile.KeychainLabel);
                continue;
            }

            try
            {
                foreach (var cookieName in cookieNames)
                {
                    var encrypted = QueryCookieEncrypted(tempCopy, cookieName, hostPatterns);
                    if (encrypted is null || encrypted.Length == 0)
                        continue;

                    var decrypted = decrypt(encryptionKey, encrypted);
                    if (!string.IsNullOrWhiteSpace(decrypted))
                        return new ChromiumCookieReadResult { Value = decrypted };

                    if (IsAppBoundEncryptedValue(encrypted))
                        hasUnreadableEncryptedCookie = true;
                }
            }
            finally
            {
                try { File.Delete(tempCopy); } catch { /* ignore */ }
            }
        }

        return new ChromiumCookieReadResult
        {
            LockedBrowsers = lockedBrowsers.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            HasUnreadableEncryptedCookie = hasUnreadableEncryptedCookie
        };
    }

    public static string? TryReadCookieFromDatabase(
        string cookieDatabasePath,
        string cookieName,
        IReadOnlyList<string> hostPatterns,
        byte[]? encryptionKey,
        Func<byte[]?, byte[], string?> decryptValue)
    {
        if (!File.Exists(cookieDatabasePath))
            return null;

        var tempCopy = Path.Combine(Path.GetTempPath(), $"chromium-cookies-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(cookieDatabasePath, tempCopy, overwrite: true);
            return QueryCookie(tempCopy, cookieName, hostPatterns, encryptionKey, decryptValue);
        }
        catch (IOException)
        {
            return null;
        }
        catch (SqliteException)
        {
            return null;
        }
        finally
        {
            try { File.Delete(tempCopy); } catch { /* ignore */ }
        }
    }

    public static byte[]? ReadChromiumEncryptionKey(string localStatePath, string? keychainLabel = null)
    {
        if (!File.Exists(localStatePath))
            return null;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(localStatePath));
            if (!document.RootElement.TryGetProperty("os_crypt", out var osCrypt)
                || !osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyEl)
                || encryptedKeyEl.ValueKind != JsonValueKind.String)
                return null;

            var encryptedKey = Convert.FromBase64String(encryptedKeyEl.GetString() ?? "");
            if (encryptedKey.Length <= 5)
                return null;

            if (encryptedKey.AsSpan(0, 5).SequenceEqual("DPAPI"u8))
            {
                if (!OperatingSystem.IsWindows())
                    return null;

                return ProtectedData.Unprotect(encryptedKey.AsSpan(5).ToArray(), null, DataProtectionScope.CurrentUser);
            }

            if (encryptedKey.AsSpan(0, 3).SequenceEqual("v10"u8))
            {
                var keychainPassword = ReadMacKeychainPassword(keychainLabel);
                if (string.IsNullOrEmpty(keychainPassword))
                    return null;

                return DecryptMacOsChromiumKey(keychainPassword, encryptedKey.AsSpan(3).ToArray());
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public static string? DecryptChromiumCookieValue(byte[]? encryptionKey, byte[] encryptedValue)
    {
        if (encryptedValue.Length == 0)
            return null;

        if (encryptionKey is { Length: > 0 }
            && encryptedValue.Length >= 3
            && encryptedValue[0] == (byte)'v'
            && encryptedValue[1] == (byte)'1'
            && (encryptedValue[2] == (byte)'0' || encryptedValue[2] == (byte)'1'))
        {
            return DecryptAesGcmCookie(encryptionKey, encryptedValue);
        }

        if (OperatingSystem.IsWindows())
        {
            try
            {
                var decrypted = ProtectedData.Unprotect(encryptedValue, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(decrypted);
            }
            catch
            {
                // fall through
            }
        }

        if (encryptionKey is null || encryptionKey.Length == 0)
            return TryDecodePlainCookieValue(encryptedValue);

        return null;
    }

    public static IReadOnlyList<ChromiumBrowserProfile> ResolveDefaultProfiles()
    {
        var profiles = new List<ChromiumBrowserProfile>();

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddBrowserProfiles(profiles, Path.Combine(localAppData, "Google", "Chrome", "User Data"), "Chrome");
            AddBrowserProfiles(profiles, Path.Combine(localAppData, "Microsoft", "Edge", "User Data"), "Microsoft Edge");
            AddBrowserProfiles(profiles, Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"), "Brave");
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var appSupport = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support");
            AddBrowserProfiles(profiles, Path.Combine(appSupport, "Google", "Chrome"), "Chrome");
            AddBrowserProfiles(profiles, Path.Combine(appSupport, "Microsoft Edge"), "Microsoft Edge");
            AddBrowserProfiles(profiles, Path.Combine(appSupport, "BraveSoftware", "Brave-Browser"), "Brave");
        }

        return profiles;
    }

    private static bool IsAppBoundEncryptedValue(byte[] encryptedValue) =>
        encryptedValue.Length >= 3
        && encryptedValue[0] == (byte)'v'
        && encryptedValue[1] == (byte)'2'
        && encryptedValue[2] == (byte)'0';

    private static byte[]? QueryCookieEncrypted(
        string databasePath,
        string cookieName,
        IReadOnlyList<string> hostPatterns)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var hostClause = string.Join(" OR ", hostPatterns.Select((_, i) => $"host_key = $host{i}"));
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT encrypted_value
             FROM cookies
             WHERE name = $name
               AND ({hostClause})
             ORDER BY length(encrypted_value) DESC
             LIMIT 1
             """;
        command.Parameters.AddWithValue("$name", cookieName);
        for (var i = 0; i < hostPatterns.Count; i++)
            command.Parameters.AddWithValue($"$host{i}", hostPatterns[i]);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
            return null;

        return (byte[])reader.GetValue(0);
    }

    private static string? QueryCookie(
        string databasePath,
        string cookieName,
        IReadOnlyList<string> hostPatterns,
        byte[]? encryptionKey,
        Func<byte[]?, byte[], string?> decryptValue)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var hostClause = string.Join(" OR ", hostPatterns.Select((_, i) => $"host_key = $host{i}"));
        using var command = connection.CreateCommand();
        command.CommandText =
            $"""
             SELECT encrypted_value
             FROM cookies
             WHERE name = $name
               AND ({hostClause})
             ORDER BY length(encrypted_value) DESC
             LIMIT 1
             """;
        command.Parameters.AddWithValue("$name", cookieName);
        for (var i = 0; i < hostPatterns.Count; i++)
            command.Parameters.AddWithValue($"$host{i}", hostPatterns[i]);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
            return null;

        var encryptedValue = (byte[])reader.GetValue(0);
        var decrypted = decryptValue(encryptionKey, encryptedValue);
        return string.IsNullOrWhiteSpace(decrypted) ? null : decrypted;
    }

    private static byte[]? DecryptMacOsChromiumKey(string password, byte[] ciphertext)
    {
        try
        {
            var derived = Rfc2898DeriveBytes.Pbkdf2(
                Encoding.UTF8.GetBytes(password),
                "saltysalt"u8,
                1003,
                HashAlgorithmName.SHA1,
                16);

            if (ciphertext.Length < 16)
                return null;

            var iv = ciphertext.AsSpan(0, 16);
            var data = ciphertext.AsSpan(16);
            var plain = new byte[data.Length];
            using var aes = Aes.Create();
            aes.Key = derived;
            aes.IV = iv.ToArray();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data.ToArray(), 0, data.Length);
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadMacKeychainPassword(string? browserLabel)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) || string.IsNullOrWhiteSpace(browserLabel))
            return null;

        var service = $"{browserLabel} Safe Storage";
        try
        {
            var startInfo = new ProcessStartInfo(
                "/usr/bin/security",
                $"find-generic-password -w -s \"{service}\" -a \"{browserLabel}\"")
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
            return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output) ? output.Trim() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? DecryptAesGcmCookie(byte[] key, byte[] encryptedValue)
    {
        if (encryptedValue.Length < 3 + 12 + 16)
            return null;

        var nonce = encryptedValue.AsSpan(3, 12);
        var cipherText = encryptedValue.AsSpan(15, encryptedValue.Length - 15 - 16);
        var tag = encryptedValue.AsSpan(encryptedValue.Length - 16, 16);

        var plain = new byte[cipherText.Length];
        try
        {
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(nonce, cipherText, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    private static string? TryDecodePlainCookieValue(byte[] encryptedValue) =>
        Encoding.UTF8.GetString(encryptedValue);

    private static void AddBrowserProfiles(List<ChromiumBrowserProfile> profiles, string userDataDir, string keychainLabel)
    {
        if (!Directory.Exists(userDataDir))
            return;

        var localStatePath = Path.Combine(userDataDir, "Local State");
        AddProfileIfPresent(profiles, userDataDir, "Default", localStatePath, keychainLabel);

        foreach (var profileDir in Directory.EnumerateDirectories(userDataDir, "Profile *"))
        {
            var profileName = Path.GetFileName(profileDir);
            AddProfileIfPresent(profiles, userDataDir, profileName, localStatePath, keychainLabel);
        }
    }

    private static void AddProfileIfPresent(
        List<ChromiumBrowserProfile> profiles,
        string userDataDir,
        string profileName,
        string localStatePath,
        string keychainLabel)
    {
        var cookiePath = Path.Combine(userDataDir, profileName, "Network", "Cookies");
        if (!File.Exists(cookiePath))
            cookiePath = Path.Combine(userDataDir, profileName, "Cookies");

        if (File.Exists(cookiePath))
            profiles.Add(new ChromiumBrowserProfile(cookiePath, localStatePath, keychainLabel));
    }
}

public sealed record ChromiumBrowserProfile(string CookieDatabasePath, string LocalStatePath, string? KeychainLabel = null);

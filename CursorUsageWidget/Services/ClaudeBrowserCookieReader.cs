using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CursorUsageWidget.Services;

public sealed class ClaudeBrowserCookieReader
{
    private const string CookieName = "sessionKey";

    private readonly Func<IReadOnlyList<ChromiumBrowserProfile>> _profileResolver;
    private readonly Func<byte[]?, byte[], string?> _decryptValue;

    public ClaudeBrowserCookieReader(
        Func<IReadOnlyList<ChromiumBrowserProfile>>? profileResolver = null,
        Func<byte[]?, byte[], string?>? decryptValue = null)
    {
        _profileResolver = profileResolver ?? ResolveDefaultProfiles;
        _decryptValue = decryptValue ?? DecryptChromiumCookieValue;
    }

    public string? ReadSessionKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return null;

        foreach (var profile in _profileResolver())
        {
            var sessionKey = TryReadSessionKeyFromProfile(profile);
            if (!string.IsNullOrWhiteSpace(sessionKey))
                return sessionKey;
        }

        return null;
    }

    internal static string? TryReadSessionKeyFromCookieDatabase(
        string cookieDatabasePath,
        byte[]? encryptionKey,
        Func<byte[]?, byte[], string?> decryptValue)
    {
        if (!File.Exists(cookieDatabasePath))
            return null;

        var tempCopy = Path.Combine(Path.GetTempPath(), $"claude-cookies-{Guid.NewGuid():N}.db");
        try
        {
            File.Copy(cookieDatabasePath, tempCopy, overwrite: true);
            return QuerySessionKey(tempCopy, encryptionKey, decryptValue);
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

    internal static byte[]? ReadChromiumEncryptionKey(string localStatePath)
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
            if (encryptedKey.Length <= 5
                || !encryptedKey.AsSpan(0, 5).SequenceEqual("DPAPI"u8))
                return null;

            return ProtectedData.Unprotect(encryptedKey.AsSpan(5).ToArray(), null, DataProtectionScope.CurrentUser);
        }
        catch
        {
            return null;
        }
    }

    internal static string? DecryptChromiumCookieValue(byte[]? encryptionKey, byte[] encryptedValue)
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

    private string? TryReadSessionKeyFromProfile(ChromiumBrowserProfile profile)
    {
        var encryptionKey = ReadChromiumEncryptionKey(profile.LocalStatePath);
        return TryReadSessionKeyFromCookieDatabase(profile.CookieDatabasePath, encryptionKey, _decryptValue);
    }

    private static string? QuerySessionKey(
        string databasePath,
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

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT encrypted_value
            FROM cookies
            WHERE name = $name
              AND (host_key = 'claude.ai' OR host_key = '.claude.ai')
            ORDER BY length(encrypted_value) DESC
            LIMIT 1
            """;
        command.Parameters.AddWithValue("$name", CookieName);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
            return null;

        var encryptedValue = (byte[])reader.GetValue(0);
        var decrypted = decryptValue(encryptionKey, encryptedValue);
        return string.IsNullOrWhiteSpace(decrypted) ? null : decrypted;
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

    private static IReadOnlyList<ChromiumBrowserProfile> ResolveDefaultProfiles()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var profiles = new List<ChromiumBrowserProfile>();

        AddBrowserProfiles(profiles, Path.Combine(localAppData, "Google", "Chrome", "User Data"));
        AddBrowserProfiles(profiles, Path.Combine(localAppData, "Microsoft", "Edge", "User Data"));
        AddBrowserProfiles(profiles, Path.Combine(localAppData, "BraveSoftware", "Brave-Browser", "User Data"));

        return profiles;
    }

    private static void AddBrowserProfiles(List<ChromiumBrowserProfile> profiles, string userDataDir)
    {
        if (!Directory.Exists(userDataDir))
            return;

        var localStatePath = Path.Combine(userDataDir, "Local State");
        AddProfileIfPresent(profiles, userDataDir, "Default", localStatePath);

        foreach (var profileDir in Directory.EnumerateDirectories(userDataDir, "Profile *"))
        {
            var profileName = Path.GetFileName(profileDir);
            AddProfileIfPresent(profiles, userDataDir, profileName, localStatePath);
        }
    }

    private static void AddProfileIfPresent(
        List<ChromiumBrowserProfile> profiles,
        string userDataDir,
        string profileName,
        string localStatePath)
    {
        var cookiePath = Path.Combine(userDataDir, profileName, "Network", "Cookies");
        if (!File.Exists(cookiePath))
            cookiePath = Path.Combine(userDataDir, profileName, "Cookies");

        if (File.Exists(cookiePath))
            profiles.Add(new ChromiumBrowserProfile(cookiePath, localStatePath));
    }
}

public sealed record ChromiumBrowserProfile(string CookieDatabasePath, string LocalStatePath);
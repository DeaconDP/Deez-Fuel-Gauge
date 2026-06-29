using System.Security.Cryptography;
using System.Text;
using DeezFuelGauge.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class ClaudeBrowserCookieReaderTests
{
    [Fact]
    public void TryReadSessionKeyFromCookieDatabase_reads_session_key()
    {
        var databasePath = CreateCookieDatabase("browser-cookie-test");

        try
        {
            var sessionKey = ClaudeBrowserCookieReader.TryReadSessionKeyFromCookieDatabase(
                databasePath,
                encryptionKey: null,
                (_, encryptedValue) => Encoding.UTF8.GetString(encryptedValue));

            Assert.Equal("abc-session-key", sessionKey);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(databasePath);
        }
    }

    [Fact]
    public void DecryptChromiumCookieValue_supports_legacy_dpapi_values()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var plain = "session-from-dpapi";
        var encrypted = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plain),
            null,
            DataProtectionScope.CurrentUser);

        var decrypted = ClaudeBrowserCookieReader.DecryptChromiumCookieValue(null, encrypted);

        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void DeriveMacChromiumKey_is_deterministic()
    {
        var key = ClaudeBrowserCookieReader.DeriveMacChromiumKey("peanuts");
        var again = ClaudeBrowserCookieReader.DeriveMacChromiumKey("peanuts");

        Assert.Equal(key, again);
        Assert.Equal(16, key.Length);
    }

    [Fact]
    public void DecryptMacV10Cookie_decrypts_aes_cbc_cookie_value()
    {
        var key = ClaudeBrowserCookieReader.DeriveMacChromiumKey("peanuts");
        var plain = Encoding.UTF8.GetBytes("abc-session-key");
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        byte[] encrypted;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            encrypted = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }

        var payload = new byte[3 + 16 + encrypted.Length];
        payload[0] = (byte)'v';
        payload[1] = (byte)'1';
        payload[2] = (byte)'0';
        iv.CopyTo(payload.AsSpan(3));
        encrypted.CopyTo(payload.AsSpan(19));

        var decrypted = ClaudeBrowserCookieReader.DecryptMacV10Cookie(key, payload);

        Assert.Equal("abc-session-key", decrypted);
    }

    [Fact]
    public void DecryptChromiumCookieValue_uses_mac_v10_cbc_on_macos()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var key = ClaudeBrowserCookieReader.DeriveMacChromiumKey("peanuts");
        var plain = Encoding.UTF8.GetBytes("session-from-browser");
        var iv = new byte[16];
        RandomNumberGenerator.Fill(iv);

        byte[] encrypted;
        using (var aes = Aes.Create())
        {
            aes.Key = key;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            encrypted = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        }

        var payload = new byte[3 + 16 + encrypted.Length];
        payload[0] = (byte)'v';
        payload[1] = (byte)'1';
        payload[2] = (byte)'0';
        iv.CopyTo(payload.AsSpan(3));
        encrypted.CopyTo(payload.AsSpan(19));

        var decrypted = ClaudeBrowserCookieReader.DecryptChromiumCookieValue(key, payload);

        Assert.Equal("session-from-browser", decrypted);
    }

    [Fact]
    public void Mac_chrome_profile_path_uses_application_support()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var chromePath = Path.Combine(localAppData, "Google", "Chrome", "User Data");

        Assert.Contains("Application Support", chromePath);
        Assert.EndsWith(Path.Combine("Google", "Chrome", "User Data"), chromePath);
    }

    [Fact]
    public void ReadChromiumEncryptionKey_returns_null_for_missing_mac_keychain_entry()
    {
        if (!OperatingSystem.IsMacOS())
            return;

        var localStatePath = Path.Combine(Path.GetTempPath(), $"local-state-{Guid.NewGuid():N}.json");
        var encryptedKeyBytes = new byte[] { (byte)'v', (byte)'1', (byte)'0', 1, 2, 3, 4 };
        var encryptedPayload = Convert.ToBase64String(encryptedKeyBytes);

        File.WriteAllText(
            localStatePath,
            "{\"os_crypt\":{\"encrypted_key\":\"" + encryptedPayload + "\"}}");

        try
        {
            var profile = new ChromiumBrowserProfile(
                CookieDatabasePath: "/tmp/nonexistent-cookies",
                LocalStatePath: localStatePath,
                MacKeychainService: "DeezFuelGauge-Tests-Missing",
                MacKeychainAccount: "Missing");

            var key = ClaudeBrowserCookieReader.ReadChromiumEncryptionKey(profile);

            Assert.Null(key);
        }
        finally
        {
            File.Delete(localStatePath);
        }
    }

    private static string CreateCookieDatabase(string name)
    {
        var path = Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.db");
        var connectionString = $"Data Source={path}";
        using (var connection = new SqliteConnection(connectionString))
        {
            connection.Open();

            using (var create = connection.CreateCommand())
            {
                create.CommandText =
                    """
                    CREATE TABLE cookies (
                      name TEXT NOT NULL,
                      host_key TEXT NOT NULL,
                      encrypted_value BLOB NOT NULL
                    );
                    """;
                create.ExecuteNonQuery();
            }

            using (var insert = connection.CreateCommand())
            {
                insert.CommandText =
                    """
                    INSERT INTO cookies (name, host_key, encrypted_value)
                    VALUES ($name, $host, $value);
                    """;
                insert.Parameters.AddWithValue("$name", "sessionKey");
                insert.Parameters.AddWithValue("$host", ".claude.ai");
                insert.Parameters.AddWithValue("$value", Encoding.UTF8.GetBytes("abc-session-key"));
                insert.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
        }

        return path;
    }
}

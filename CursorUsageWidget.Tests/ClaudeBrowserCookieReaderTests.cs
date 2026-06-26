using CursorUsageWidget.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CursorUsageWidget.Tests;

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
                (_, encryptedValue) => System.Text.Encoding.UTF8.GetString(encryptedValue));

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
        var encrypted = System.Security.Cryptography.ProtectedData.Protect(
            System.Text.Encoding.UTF8.GetBytes(plain),
            null,
            System.Security.Cryptography.DataProtectionScope.CurrentUser);

        var decrypted = ClaudeBrowserCookieReader.DecryptChromiumCookieValue(null, encrypted);

        Assert.Equal(plain, decrypted);
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
                insert.Parameters.AddWithValue("$value", System.Text.Encoding.UTF8.GetBytes("abc-session-key"));
                insert.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
        }

        return path;
    }
}

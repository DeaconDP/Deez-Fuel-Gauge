using DeezFuelGauge.Services;
using Microsoft.Data.Sqlite;
using Xunit;

namespace DeezFuelGauge.Tests;

public sealed class CursorTokenReaderTests
{
    [Fact]
    public void ReadFromPath_reads_tokens_from_sqlite()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"cursor-token-test-{Guid.NewGuid():N}.vscdb");
        try
        {
            CreateTestDatabase(dbPath, accessToken: "access-123", refreshToken: "refresh-456");
            var tokens = CursorTokenReader.ReadFromPath(dbPath);

            Assert.Equal("access-123", tokens.AccessToken);
            Assert.Equal("refresh-456", tokens.RefreshToken);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(dbPath))
                File.Delete(dbPath);
        }
    }

    private static void CreateTestDatabase(string path, string accessToken, string refreshToken)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "CREATE TABLE ItemTable (key TEXT PRIMARY KEY, value TEXT)";
            command.ExecuteNonQuery();
        }

        InsertValue(connection, "cursorAuth/accessToken", accessToken);
        InsertValue(connection, "cursorAuth/refreshToken", refreshToken);
    }

    private static void InsertValue(SqliteConnection connection, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO ItemTable (key, value) VALUES ($key, $value)";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        command.ExecuteNonQuery();
    }
}

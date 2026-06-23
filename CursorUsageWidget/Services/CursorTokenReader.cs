using System.IO;
using Microsoft.Data.Sqlite;

namespace CursorUsageWidget.Services;

public sealed class CursorTokens
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
}

public static class CursorTokenReader
{
    private const string AccessTokenKey = "cursorAuth/accessToken";
    private const string RefreshTokenKey = "cursorAuth/refreshToken";

    public static string DatabasePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Cursor", "User", "globalStorage", "state.vscdb");

    public static CursorTokens Read()
    {
        var dbPath = DatabasePath;
        if (!File.Exists(dbPath))
            return new CursorTokens();

        try
        {
            return ReadFromPath(dbPath);
        }
        catch (SqliteException)
        {
            var tempCopy = Path.Combine(Path.GetTempPath(), $"cursor-state-{Guid.NewGuid():N}.vscdb");
            try
            {
                File.Copy(dbPath, tempCopy, overwrite: true);
                return ReadFromPath(tempCopy);
            }
            finally
            {
                try { File.Delete(tempCopy); } catch { /* ignore */ }
            }
        }
    }

    private static CursorTokens ReadFromPath(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        return new CursorTokens
        {
            AccessToken = ReadValue(connection, AccessTokenKey),
            RefreshToken = ReadValue(connection, RefreshTokenKey)
        };
    }

    private static string? ReadValue(SqliteConnection connection, string key)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
        command.Parameters.AddWithValue("$key", key);
        var result = command.ExecuteScalar();
        return result as string;
    }
}

using System.IO;
using Microsoft.Data.Sqlite;

namespace DeezFuelGauge.Services;

public static class AntigravityTokenReader
{
    private const string OAuthTokenKey = "antigravityUnifiedStateSync.oauthToken";

    public static AntigravityOAuthTokens Read()
    {
        foreach (var dbPath in PlatformPaths.AntigravityStateDatabasePaths)
        {
            if (!File.Exists(dbPath))
                continue;

            try
            {
                var tokens = ReadFromPath(dbPath);
                if (!string.IsNullOrWhiteSpace(tokens.AccessToken) || !string.IsNullOrWhiteSpace(tokens.RefreshToken))
                    return tokens;
            }
            catch (SqliteException)
            {
                var tempCopy = Path.Combine(Path.GetTempPath(), $"antigravity-state-{Guid.NewGuid():N}.vscdb");
                try
                {
                    File.Copy(dbPath, tempCopy, overwrite: true);
                    var tokens = ReadFromPath(tempCopy);
                    if (!string.IsNullOrWhiteSpace(tokens.AccessToken) || !string.IsNullOrWhiteSpace(tokens.RefreshToken))
                        return tokens;
                }
                finally
                {
                    try { File.Delete(tempCopy); } catch { /* ignore */ }
                }
            }
            catch
            {
                // try next path
            }
        }

        return new AntigravityOAuthTokens();
    }

    internal static AntigravityOAuthTokens ParseOAuthEnvelope(string? envelopeValue)
    {
        if (string.IsNullOrWhiteSpace(envelopeValue))
            return new AntigravityOAuthTokens();

        try
        {
            var outer = Convert.FromBase64String(envelopeValue.Trim());
            var wrapper = ProtobufWireParser.ReadBytesField(outer, 1);
            if (wrapper.IsEmpty)
                return new AntigravityOAuthTokens();

            var payload = ProtobufWireParser.ReadBytesField(wrapper, 2);
            if (payload.IsEmpty)
                return new AntigravityOAuthTokens();

            var innerBase64 = ProtobufWireParser.ReadStringField(payload, 1);
            if (string.IsNullOrWhiteSpace(innerBase64))
                return new AntigravityOAuthTokens();

            var tokenInfo = Convert.FromBase64String(innerBase64);
            var accessToken = ProtobufWireParser.ReadStringField(tokenInfo, 1);
            var refreshToken = ProtobufWireParser.ReadStringField(tokenInfo, 3);
            var expirySeconds = ProtobufWireParser.ReadTimestampSeconds(tokenInfo, 4);

            DateTimeOffset? expiresAt = expirySeconds is > 0
                ? DateTimeOffset.FromUnixTimeSeconds(expirySeconds.Value)
                : null;

            return new AntigravityOAuthTokens
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };
        }
        catch
        {
            return new AntigravityOAuthTokens();
        }
    }

    private static AntigravityOAuthTokens ReadFromPath(string path)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM ItemTable WHERE key = $key";
        command.Parameters.AddWithValue("$key", OAuthTokenKey);
        var result = command.ExecuteScalar() as string;
        return ParseOAuthEnvelope(result);
    }
}

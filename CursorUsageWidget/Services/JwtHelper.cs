using System.Text;
using System.Text.Json;

namespace CursorUsageWidget.Services;

public static class JwtHelper
{
    public static string? GetSubject(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length < 2)
                return null;

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/')));

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("sub", out var subEl))
                return null;

            return subEl.GetString();
        }
        catch
        {
            return null;
        }
    }

    public static string? GetSessionUserId(string jwt)
    {
        var sub = GetSubject(jwt);
        if (string.IsNullOrWhiteSpace(sub))
            return null;

        var pipeIndex = sub.LastIndexOf('|');
        return pipeIndex >= 0 && pipeIndex < sub.Length - 1
            ? sub[(pipeIndex + 1)..]
            : sub;
    }
}

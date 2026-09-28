using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace DeezFuelGauge.Services;

/// <summary>
/// Runtime probe for compact HWND vs rest drift. Enable with DEEZ_COMPACT_TRACE=1.
/// Writes JSON lines under %TEMP%\deez-compact-trace.jsonl.
/// </summary>
internal static class CompactGeometryTrace
{
    private static readonly bool Enabled =
        string.Equals(Environment.GetEnvironmentVariable("DEEZ_COMPACT_TRACE"), "1", StringComparison.Ordinal);

    private static readonly string TracePath = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "deez-compact-trace.jsonl");

    private static readonly object Gate = new();

    public static void Event(
        string name,
        int positionX,
        int positionY,
        double boundsW,
        double boundsH,
        int? restX,
        int? restY,
        double pillW,
        double pillH,
        double progress,
        bool pointerOver,
        bool regionCleared = false)
    {
        if (!Enabled)
            return;

        var pillX = positionX + boundsW - pillW;
        var pillY = positionY + boundsH - pillH;
        var hostEqualsRest = restX is int rx && restY is int ry
            && positionX == rx && positionY == ry
            && Math.Abs(boundsW - pillW) < 1.5
            && Math.Abs(boundsH - pillH) < 1.5;

        var sb = new StringBuilder(256);
        sb.Append('{');
        Append(sb, "t", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), first: true);
        Append(sb, "event", name);
        Append(sb, "posX", positionX);
        Append(sb, "posY", positionY);
        Append(sb, "boundsW", boundsW);
        Append(sb, "boundsH", boundsH);
        Append(sb, "restX", restX);
        Append(sb, "restY", restY);
        Append(sb, "pillW", pillW);
        Append(sb, "pillH", pillH);
        Append(sb, "pillX", pillX);
        Append(sb, "pillY", pillY);
        Append(sb, "progress", progress);
        Append(sb, "pointerOver", pointerOver);
        Append(sb, "regionCleared", regionCleared);
        Append(sb, "hostEqualsRest", hostEqualsRest);
        sb.Append('}');

        lock (Gate)
        {
            try
            {
                File.AppendAllText(TracePath, sb.ToString() + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _ = ex;
            }
        }

        Debug.WriteLine(sb.ToString());
    }

    private static void Append(StringBuilder sb, string key, object? value, bool first = false)
    {
        if (!first)
            sb.Append(',');
        sb.Append('"').Append(key).Append("\":");
        switch (value)
        {
            case null:
                sb.Append("null");
                break;
            case bool b:
                sb.Append(b ? "true" : "false");
                break;
            case string s:
                sb.Append('"').Append(s.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
                break;
            case IFormattable f:
                sb.Append(f.ToString(null, CultureInfo.InvariantCulture));
                break;
            default:
                sb.Append('"').Append(value).Append('"');
                break;
        }
    }
}

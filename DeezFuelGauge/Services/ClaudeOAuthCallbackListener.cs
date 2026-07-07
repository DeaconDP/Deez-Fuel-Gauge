using System.Net;
using System.Net.Sockets;
using System.Text;

namespace DeezFuelGauge.Services;

public sealed record ClaudeOAuthCallbackResult(string? Code, string? State, string? Error);

/// <summary>
/// Minimal single-shot HTTP listener for the OAuth loopback redirect
/// (http://localhost:{port}/callback) — the same flow the Claude Code CLI uses by
/// default. TcpListener is used instead of HttpListener so no URL ACL is needed.
/// </summary>
public sealed class ClaudeOAuthCallbackListener : IDisposable
{
    private readonly TcpListener _listener;

    public int Port { get; }

    private ClaudeOAuthCallbackListener(TcpListener listener, int port)
    {
        _listener = listener;
        Port = port;
    }

    public static ClaudeOAuthCallbackListener? TryStart(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
            var boundPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            return new ClaudeOAuthCallbackListener(listener, boundPort);
        }
        catch (SocketException)
        {
            return null;
        }
    }

    public async Task<ClaudeOAuthCallbackResult> WaitForCallbackAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = await _listener.AcceptTcpClientAsync(cancellationToken);
            var result = await HandleConnectionAsync(client, cancellationToken);
            if (result is not null)
                return result;
        }
    }

    internal static ClaudeOAuthCallbackResult? ParseRequestLine(string requestLine)
    {
        var parts = requestLine.Split(' ');
        if (parts.Length < 2 || parts[0] != "GET")
            return null;

        var queryStart = parts[1].IndexOf('?');
        if (queryStart < 0)
            return null;

        string? code = null, state = null, error = null;
        foreach (var pair in parts[1][(queryStart + 1)..].Split('&'))
        {
            var eq = pair.IndexOf('=');
            if (eq < 0)
                continue;

            var key = pair[..eq];
            var value = Uri.UnescapeDataString(pair[(eq + 1)..]);
            switch (key)
            {
                case "code": code = value; break;
                case "state": state = value; break;
                case "error": error = value; break;
            }
        }

        return code is null && error is null
            ? null
            : new ClaudeOAuthCallbackResult(code, state, error);
    }

    private static async Task<ClaudeOAuthCallbackResult?> HandleConnectionAsync(
        TcpClient client,
        CancellationToken cancellationToken)
    {
        var stream = client.GetStream();
        var buffer = new byte[8192];
        var read = await stream.ReadAsync(buffer, cancellationToken);
        if (read <= 0)
            return null;

        var request = Encoding.ASCII.GetString(buffer, 0, read);
        var lineEnd = request.IndexOf('\r');
        var requestLine = lineEnd > 0 ? request[..lineEnd] : request;
        var result = ParseRequestLine(requestLine);

        var body = result is null
            ? "<html><body>Not found.</body></html>"
            : result.Error is not null
                ? "<html><body style=\"font-family:sans-serif\"><h2>Sign-in was not completed</h2><p>You can close this tab and try again in Deez Fuel Gauge.</p></body></html>"
                : "<html><body style=\"font-family:sans-serif\"><h2>Signed in</h2><p>You can close this tab and return to Deez Fuel Gauge.</p></body></html>";

        var status = result is null ? "404 Not Found" : "200 OK";
        var response =
            $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(response), cancellationToken);
        await stream.FlushAsync(cancellationToken);

        return result;
    }

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch
        {
            // ignore
        }
    }
}

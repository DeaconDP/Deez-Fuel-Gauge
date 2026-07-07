using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DeezFuelGauge.Models;

namespace DeezFuelGauge.Services;

public sealed record ClaudeOAuthLoginStart(string AuthorizeUrl, string CodeVerifier, string State, string RedirectUri);

/// <summary>
/// Implements the same OAuth 2.0 + PKCE "authorization code" flow the official Claude Code
/// CLI uses for `claude login` (client_id, endpoints, and scopes are publicly known from the
/// CLI's own binary). This lets the widget sign in to a user's Claude account directly,
/// without depending on the CLI being installed or on scraping another app's local storage.
/// </summary>
public sealed class ClaudeOAuthLoginService : IDisposable
{
    private const string ClientId = "9d1c250a-e61b-44d9-88ed-5944d1962f5e";

    // These constants mirror `claude auth login --claudeai` exactly (verified working
    // July 2026). The claude.com/cai host matters: the same request served from
    // claude.ai/oauth/authorize is rejected with 400 "Invalid request format".
    private const string AuthorizeUrl = "https://claude.com/cai/oauth/authorize";
    private const string TokenUrl = "https://platform.claude.com/v1/oauth/token";

    // Manual copy-paste fallback; the primary flow uses the loopback redirect below,
    // matching the Claude Code CLI's default (`http://localhost:3118/callback`).
    private const string ManualRedirectUri = "https://platform.claude.com/oauth/code/callback";

    public const int DefaultCallbackPort = 3118;

    public static string BuildLoopbackRedirectUri(int port) => $"http://localhost:{port}/callback";

    private const string Scopes =
        "org:create_api_key user:profile user:inference user:sessions:claude_code user:mcp_servers user:file_upload";

    private readonly HttpClient _http;
    private readonly bool _ownsHttp;

    public ClaudeOAuthLoginService(HttpClient? http = null)
    {
        _ownsHttp = http is null;
        _http = http ?? new HttpClient();
    }

    public ClaudeOAuthLoginStart BeginLogin(string? redirectUri = null)
    {
        var codeVerifier = GenerateUrlSafeToken(32);
        var codeChallenge = ComputeCodeChallenge(codeVerifier);
        var state = GenerateUrlSafeToken(16);
        var redirect = redirectUri ?? ManualRedirectUri;

        var query = string.Join('&', new[]
        {
            "code=true",
            $"client_id={Uri.EscapeDataString(ClientId)}",
            "response_type=code",
            $"redirect_uri={Uri.EscapeDataString(redirect)}",
            $"scope={Uri.EscapeDataString(Scopes)}",
            $"code_challenge={Uri.EscapeDataString(codeChallenge)}",
            "code_challenge_method=S256",
            $"state={Uri.EscapeDataString(state)}"
        });

        return new ClaudeOAuthLoginStart($"{AuthorizeUrl}?{query}", codeVerifier, state, redirect);
    }

    public async Task<ClaudeOAuthToken> ExchangeCodeAsync(
        string pastedCode,
        string codeVerifier,
        string expectedState,
        string? redirectUri = null,
        CancellationToken cancellationToken = default)
    {
        var (code, state) = SplitPastedCode(pastedCode);
        if (string.IsNullOrWhiteSpace(code))
            throw new ClaudeOAuthException("Paste the full code from claude.ai (looks like \"code#state\")");

        if (!string.IsNullOrEmpty(state) && !string.Equals(state, expectedState, StringComparison.Ordinal))
            throw new ClaudeOAuthException("That code doesn't match this sign-in attempt — click Sign in with Claude again");

        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri ?? ManualRedirectUri,
            ["client_id"] = ClientId,
            ["code_verifier"] = codeVerifier,
            ["state"] = expectedState
        };

        return await RequestTokenAsync(payload, cancellationToken);
    }

    public async Task<ClaudeOAuthToken> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = ClientId
        };

        return await RequestTokenAsync(payload, cancellationToken);
    }

    internal static (string Code, string State) SplitPastedCode(string pastedText)
    {
        var trimmed = pastedText.Trim();
        var hashIndex = trimmed.IndexOf('#');
        return hashIndex < 0
            ? (trimmed, "")
            : (trimmed[..hashIndex], trimmed[(hashIndex + 1)..]);
    }

    internal static string GenerateUrlSafeToken(int byteCount) =>
        Base64UrlEncode(RandomNumberGenerator.GetBytes(byteCount));

    internal static string ComputeCodeChallenge(string codeVerifier) =>
        Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(codeVerifier)));

    internal static ClaudeOAuthToken ParseTokenResponse(JsonElement root)
    {
        var accessToken = root.TryGetProperty("access_token", out var accessTokenEl)
            ? accessTokenEl.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(accessToken))
            throw new ClaudeOAuthException("Sign-in response was missing an access token");

        var refreshToken = root.TryGetProperty("refresh_token", out var refreshTokenEl)
            ? refreshTokenEl.GetString()
            : null;

        var expiresInSeconds = root.TryGetProperty("expires_in", out var expiresInEl)
            && expiresInEl.ValueKind == JsonValueKind.Number
                ? expiresInEl.GetInt64()
                : 3600;

        return new ClaudeOAuthToken
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUnixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + expiresInSeconds * 1000
        };
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private async Task<ClaudeOAuthToken> RequestTokenAsync(
        Dictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(payload)
        };

        using var response = await _http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw ClaudeOAuthException.FromResponse(response.StatusCode, body);

        using var document = JsonDocument.Parse(body);
        return ParseTokenResponse(document.RootElement);
    }

    public void Dispose()
    {
        if (_ownsHttp)
            _http.Dispose();
    }
}

public sealed class ClaudeOAuthException : Exception
{
    public ClaudeOAuthException(string message) : base(message) { }

    public static ClaudeOAuthException FromResponse(HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var descEl) && descEl.ValueKind == JsonValueKind.String)
                return new ClaudeOAuthException(descEl.GetString() ?? status.ToString());

            if (doc.RootElement.TryGetProperty("error", out var errorEl))
            {
                var text = errorEl.ValueKind == JsonValueKind.String
                    ? errorEl.GetString()
                    : errorEl.TryGetProperty("message", out var msgEl) ? msgEl.GetString() : null;
                if (!string.IsNullOrWhiteSpace(text))
                    return new ClaudeOAuthException(text);
            }
        }
        catch
        {
            // ignore
        }

        return new ClaudeOAuthException($"Sign-in failed (HTTP {(int)status})");
    }
}

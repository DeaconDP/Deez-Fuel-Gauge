<img width="386" height="486" alt="2026-07-07 12_32_53-" src="https://github.com/user-attachments/assets/7433381f-f041-4dca-8102-34a8f412f2b8" />

# Cursor Usage Widget

A lightweight, draggable desktop overlay that shows how much of your Cursor included usage cap you've used.

Runs on **Windows 10/11** and **macOS**.

## Requirements

- Windows 10/11 or macOS 12+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Cursor IDE logged in on the same user profile

## One-click setup & run

Double-click the launcher for your platform:

| Platform | File |
|----------|------|
| Windows | **`setup-and-run.bat`** |
| macOS | **`setup-and-run.app`** |

On first run it builds the widget and launches it. Later runs rebuild and start the widget.

After the first successful setup, you can open **`DeezFuelGauge.app`** directly.

- **Windows:** if .NET 8 is missing, the launcher can install it via winget.
- **macOS:** if .NET 8 is missing, the launcher opens the official download page in your browser.
- **macOS:** if macOS blocks the launcher the first time, right-click **`setup-and-run.app`** and choose **Open**.
- **macOS:** if **`DeezFuelGauge.app`** is blocked after setup, right-click it and choose **Open** as well.

If setup fails on macOS, details are saved to `~/Library/Logs/DeezFuelGauge/setup.log`.

## Usage

- **Drag** the pill anywhere on screen.
- **Right-click** for Refresh or Quit.
- Position is saved locally (see paths below).
- Usage refreshes automatically every 5 minutes.

### Settings (gear icon)

Click the **gear** in the widget header to configure what is shown:

| Provider | Cursor usage (automatic) | Direct API usage (optional) |
|----------|--------------------------|-----------------------------|
| **Cursor** | Sign in to Cursor IDE on this machine — no API key needed | — |
| **OpenAI** | Aggregated from your Cursor plan | **Codex limits** (ChatGPT Plus/Pro 5h + weekly) via `~/.codex/auth.json` or session cookie; optional **Platform** Admin API key + budget |
| **Gemini** | Aggregated from your Cursor plan | **Gemini limits** (5h + weekly) via Antigravity IDE or Gemini CLI (`gemini login` → `~/.gemini/oauth_creds.json`) |

- Toggle **Show Cursor usage** / **Show Codex limits** / **Show Gemini limits** / **Show direct API usage** independently per provider.
- **Easy setup** (per provider section) turns on subscription-limit bars, checks local auth, runs the same connection tests as **Test**, and opens login pages or `codex login` when manual steps are still needed.
- **Spend details** shows remaining quota (Cursor) or dollar/token breakdown (direct).
- Use **Test** buttons to verify API keys without waiting for the 5-minute refresh.
- API keys are stored encrypted under the settings folder (`credentials/`). They are never written to `settings.json` or committed to Git.

**OpenAI (Codex / ChatGPT Plus):** if you use the [Codex CLI](https://developers.openai.com/codex), run `codex login` once — the widget reads `~/.codex/auth.json` automatically and shows the same 5-hour and weekly limits as ChatGPT's Usage & billing page. If auth is stored in the OS keyring instead, paste a ChatGPT session cookie from DevTools as a fallback. This uses an undocumented ChatGPT endpoint and may change without notice.

**OpenAI (Platform, optional):** create an [organization admin key](https://platform.openai.com) with `api.usage.read` scope for org spend tracking against a monthly budget. This is separate from ChatGPT/Codex subscription limits.

**Gemini (limits):** sign in to **Antigravity IDE** on this machine, or run **`gemini login`** with the [Gemini CLI](https://github.com/google-gemini/gemini-cli) (`npm i -g @google/gemini-cli`). Connect tries Gemini CLI first when installed, otherwise launches Antigravity IDE. The widget reads your local OAuth session and shows grouped **Gemini Models** and **Claude and GPT models** 5-hour and weekly limits (Antigravity), or per-model Gemini CLI quotas as a fallback. No API keys or project IDs needed. Uses undocumented Google Cloud Code endpoints and may change without notice.

### Settings location

| Platform | Path |
|----------|------|
| Windows | `%LOCALAPPDATA%\deez-fuel-gauge\settings.json` |
| macOS | `~/Library/Application Support/deez-fuel-gauge/settings.json` |

Encrypted API keys: `credentials/` in the same folder.

## Optional: run at login

**Windows**

1. Build the widget once using **`setup-and-run.bat`**.
2. Press `Win+R`, type `shell:startup`, press Enter.
3. Create a shortcut to `DeezFuelGauge\bin\Release\net8.0\DeezFuelGauge.exe` in that folder.

**macOS**

1. Build the widget once using **`setup-and-run.app`**.
2. Open **System Settings → General → Login Items**.
3. Add **`DeezFuelGauge.app`** from this folder.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database:
   - **Windows:** `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
   - **macOS:** `~/Library/Application Support/Cursor/User/globalStorage/state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans), or falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.
3. Optionally enriches OpenAI / Gemini bars from Cursor's aggregated usage events.
4. Optionally fetches **Codex / ChatGPT Plus** 5-hour and weekly limits from `chatgpt.com` when Codex auth or a session cookie is available.
5. Optionally fetches **Antigravity** grouped Gemini and third-party 5-hour and weekly limits from Google Cloud Code when Antigravity is signed in locally.
6. Optionally fetches **direct** OpenAI Platform billing via provider admin APIs when configured in settings.

## Disclaimer

- Uses **undocumented** Cursor endpoints that may change or break without notice.
- Not affiliated with or endorsed by Cursor.
- Your access token is read locally and sent only to `api2.cursor.sh` over HTTPS. It is never stored by this widget.

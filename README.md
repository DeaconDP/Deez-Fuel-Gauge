# Deez Fuel Gauge

A lightweight, draggable desktop overlay that shows how much of your Cursor included usage cap you've used.

Runs on **Windows 10/11** and **macOS**.

## Download

| Platform | File | Notes |
|----------|------|-------|
| Windows | [deez-fuel-gauge-win-Setup.exe](Releases/deez-fuel-gauge-win-Setup.exe) | Installer |
| Windows | [deez-fuel-gauge-win-Portable.zip](Releases/deez-fuel-gauge-win-Portable.zip) | Portable zip |
| macOS | **GitHub Releases** → `deez-fuel-gauge-mac-Universal.zip` | Universal `.app` (Apple Silicon + Intel); built by the release workflow |

To build the macOS zip locally (produces `Releases/deez-fuel-gauge-mac-Universal.zip`), run **`scripts/package-macos-release.sh`** from the repo root on a Mac with the .NET 8 SDK installed.

After downloading or building the macOS zip, unzip it and move **`Deez Fuel Gauge.app`** anywhere you like (for example **Applications**). The app is self-contained and does **not** require .NET to be installed.

If macOS blocks the app the first time, right-click **`Deez Fuel Gauge.app`** and choose **Open**.

## Requirements

- Windows 10/11 or macOS 12+
- Cursor IDE logged in on the same user profile
- **From source:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (not required for the macOS release zip above)

## One-click setup & run (from source)

Double-click the launcher for your platform:

| Platform | File |
|----------|------|
| Windows | **`setup-and-run.bat`** |
| macOS | **`setup-and-run.app`** |

On first run it builds the widget and launches it. Later runs rebuild and start the widget.

After the first successful setup, you can open **`Deez Fuel Gauge.app`** directly.

- **Windows:** if .NET 8 is missing, the launcher can install it via winget.
- **macOS:** if .NET 8 is missing, the launcher tries a silent SDK install first, then opens the official download page if that fails.
- **macOS:** if macOS blocks the launcher the first time, right-click **`setup-and-run.app`** and choose **Open**.
- **macOS:** if **`Deez Fuel Gauge.app`** is blocked after setup, right-click it and choose **Open** as well.

If setup fails on macOS, details are saved to `~/Library/Logs/DeezFuelGauge/setup.log`.

If you upgraded from Cursor Usage Widget, settings are copied automatically from `cursor-usage-widget` to `deez-fuel-gauge` on first launch. If launch-at-login was enabled, toggle it once in settings to refresh the login item.

### macOS troubleshooting

| Issue | What to try |
|-------|-------------|
| “App can’t be opened” / Gatekeeper | Right-click the app → **Open**, or **System Settings → Privacy & Security → Open Anyway** |
| Setup fails immediately | Open `~/Library/Logs/DeezFuelGauge/setup.log` for the exact error |
| .NET SDK missing | Re-run **`setup-and-run.app`** (it attempts auto-install) or install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) manually |
| Claude browser session not detected | Ensure Chrome, Edge, or Brave is logged into claude.ai; macOS may prompt for Keychain access once per browser |

## Usage

- **Drag** the pill anywhere on screen.
- **Right-click** for Refresh or Quit.
- Position is saved locally (see paths below).
- Usage refreshes automatically every 5 minutes.

### Settings (gear icon)

Click the **gear** in the widget header to configure what is shown. Settings are grouped under **Usage monitors** (AI providers) and **System** (disk space).

| Provider | Cursor usage (automatic) | Direct API usage (optional) |
|----------|--------------------------|-----------------------------|
| **Cursor** | Sign in to Cursor IDE on this machine — no API key needed | — |
| **OpenAI** | Aggregated from your Cursor plan | **Codex limits** (ChatGPT Plus/Pro 5h + weekly) via `~/.codex/auth.json` or session cookie; optional **Platform** Admin API key + budget |
| **Claude** | Aggregated from your Cursor plan | **Pro/Max limits** via Setup, Open + Refresh (Claude Code login, Chrome/Edge session, or saved session); optional **API Console** Admin key + budget |
| **Gemini** | Aggregated from your Cursor plan | **Antigravity limits** (Gemini Models + Claude/GPT groups, 5h + weekly) auto-read from local Antigravity login |
| **OpenRouter** | — | **Credits & limits** via OpenRouter API key |
| **OpenCode** | — | **Zen** credits and **Go** subscription limits via opencode.ai auth cookie + workspace ID |
| **Disk** | — | Local drive free-space bars (no API key) |

**Toggle labels** (same pattern in every provider card):

- **Widget** — show Cursor-plan spend for that provider in the widget.
- **Show** — show an optional source (Codex, Platform API, Claude Pro, Antigravity, OpenRouter, OpenCode Zen/Go, or disk drives).
- **Details** — spend details (remaining quota for Cursor plan, dollar/token breakdown for direct sources).
- **Usage breakdown** — Cursor-only Auto/API breakdown rows.
- **5h / week** (or **5h/wk/mo** for OpenCode Go) — period breakdown bars.

- Toggle visibility independently per source using **Widget**, **Show**, and **Details** as above.
- **Easy setup** (per section) turns on subscription-limit bars, checks local auth, runs the same connection tests as **Test**, and opens login pages when manual steps are still needed.
- **Spend details** shows remaining quota (Cursor) or dollar/token breakdown (direct).
- Use **Test** buttons to verify API keys without waiting for the 5-minute refresh.
- API keys are stored encrypted under the settings folder (`credentials/`). They are never written to `settings.json` or committed to Git.

**OpenAI (Codex / ChatGPT Plus):** if you use the [Codex CLI](https://developers.openai.com/codex), run `codex login` once — the widget reads `~/.codex/auth.json` automatically and shows the same 5-hour and weekly limits as ChatGPT's Usage & billing page. If auth is stored in the OS keyring instead, paste a ChatGPT session cookie from DevTools as a fallback. This uses an undocumented ChatGPT endpoint and may change without notice.

**OpenAI (Platform, optional):** create an [organization admin key](https://platform.openai.com) with `api.usage.read` scope for org spend tracking against a monthly budget. This is separate from ChatGPT/Codex subscription limits.

**Claude (Pro/Max):** click **Open** in settings to sign in at claude.ai, then click **Refresh**. The widget reads your session from Chrome, Edge, or Brave automatically on **Windows and macOS**, from Claude Code login (`~/.claude/.credentials.json` or macOS Keychain) when present, or from a saved session. macOS may show a one-time Keychain prompt when reading browser cookies. If Refresh fails, close your browser and try again. Uses undocumented claude.ai endpoints and may change without notice.

**Claude (API Console, advanced):** create an [Admin API key](https://console.anthropic.com) (`sk-ant-admin...`) and optional monthly budget for org spend tracking. This is separate from Claude Pro/Max subscription limits.

**Gemini (Antigravity):** sign in to [Antigravity](https://antigravity.google/) on this machine once. The widget reads your local Antigravity OAuth session and shows the same grouped **Gemini Models** and **Claude and GPT models** 5-hour and weekly limits as Antigravity's Model Quota screen. No API keys or project IDs needed. Uses undocumented Google Cloud Code endpoints and may change without notice.

**OpenRouter:** create an API key at [openrouter.ai](https://openrouter.ai), paste it in settings, and click **Setup** or **Test**. Shows credit balance and usage limits when available.

**OpenCode (Zen + Go):** sign in at [opencode.ai](https://opencode.ai), copy your auth cookie from DevTools and the workspace ID from the URL into settings, then click **Setup** or **Test**. **Zen** shows credit balance; **Go** shows rolling, weekly, and monthly subscription limits. Uses undocumented opencode.ai endpoints and may change without notice.

**Disk:** click **Setup** under **System → Disk** to enable local drive free-space bars. No credentials required.

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
3. Add **`Deez Fuel Gauge.app`** from this folder.

You can also enable **Launch at login** from **Settings → System → Widget** in the app.

## For developers

### Regenerate app icons

On macOS with Python 3 and Pillow installed:

```bash
pip install Pillow
python3 scripts/generate-app-icons.py
```

Source artwork lives in `packaging/icons/app-icon-source.png` (1024×1024). The script writes `packaging/icons/app-icon.png`, `app-icon.ico`, and `AppIcon.icns`. Commit the generated files — CI does not regenerate them.

### Windows release zip

```powershell
.\scripts\package-windows-release.ps1
```

Produces `Releases/deez-fuel-gauge-win-Portable.zip`.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database:
   - **Windows:** `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
   - **macOS:** `~/Library/Application Support/Cursor/User/globalStorage/state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans), or falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.
3. Optionally enriches OpenAI / Claude / Gemini bars from Cursor's aggregated usage events.
4. Optionally fetches **Codex / ChatGPT Plus** 5-hour and weekly limits from `chatgpt.com` when Codex auth or a session cookie is available.
5. Optionally fetches **Claude Pro/Max** session and weekly limits via Claude Code OAuth, browser session detection, or a saved session when configured.
6. Optionally fetches **Antigravity** grouped Gemini and Claude/GPT 5-hour and weekly limits from Google Cloud Code when Antigravity is signed in locally.
7. Optionally fetches **direct** OpenAI Platform / Claude API Console billing via provider admin APIs when configured in settings.

## Disclaimer

- Uses **undocumented** Cursor endpoints that may change or break without notice.
- Not affiliated with or endorsed by Cursor.
- Your access token is read locally and sent only to `api2.cursor.sh` over HTTPS. It is never stored by this widget.

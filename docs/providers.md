# Provider setup

Configure sources from the **gear** in the widget header. Toggle **Cursor**, **Codex / Claude.ai / Gemini App**, and **API** independently per provider.

| Provider | Cursor usage (automatic) | Subscription / app limits | Platform API (optional) |
|----------|--------------------------|---------------------------|-------------------------|
| **Cursor** | Sign in to Cursor IDE on this machine — no API key needed | **Grok Bot** allowance (Ultra / Premium) via the same login — shown with Cursor Models & Cursor API | — |
| **OpenAI** | Aggregated from your Cursor plan | **Codex** (ChatGPT Plus/Pro 5h + weekly + ChatGPT credits) via `~/.codex/auth.json` or session cookie | **OpenAI API** prepaid credit balance when available; otherwise Admin key (`api.usage.read`) + monthly budget |
| **Claude** | — | **Claude.ai** plan windows | **Claude API** Console Admin key + budget |
| **Gemini** | Aggregated from your Cursor plan | **Gemini App** limits (5h + weekly) via Antigravity IDE or Gemini CLI (`gemini login` → `~/.gemini/oauth_creds.json`) | Not metered yet |
| **fal.ai** | — | — | Prepaid **credit balance remaining** via Admin API key |

- **Easy setup** (per provider section) turns on subscription-limit bars, checks local auth, runs the same connection tests as **Test**, and opens login pages or `codex login` when manual steps are still needed.
- **Spend details** shows remaining quota (Cursor) or dollar/token breakdown (API).
- Use **Test** buttons to verify API keys without waiting for the 5-minute refresh.
- API keys are stored encrypted under the settings folder (`credentials/`). They are never written to `settings.json` or committed to Git.

## Grok Bot

Included with **Cursor Ultra** / eligible **Premium** team seats (and SuperGrok Heavy). Uses the same Cursor IDE login as the Cursor bar — no separate API key. Shown in Settings under **Cursor → Grok Bot**, and on the overlay as a third row beside **Cursor Models** and **Cursor API**. Usage percent and reset come from Cursor’s undocumented `GetSandUsageStatus` API (`api2.cursor.sh`). This bucket is separate from Cursor Auto/API monthly pools. Endpoint may change without notice.

## OpenAI (Codex / ChatGPT)

If you use the [Codex CLI](https://developers.openai.com/codex), run `codex login` once — the widget reads `~/.codex/auth.json` automatically and shows the same 5-hour and weekly limits as ChatGPT's Usage & billing page (including ChatGPT plan credits). If auth is stored in the OS keyring instead, paste a ChatGPT session cookie from DevTools as a fallback. This uses an undocumented ChatGPT endpoint and may change without notice. Separate from OpenAI API credits.

## OpenAI API (optional)

Tries prepaid credit balance via an undocumented `credit_grants` endpoint (best-effort; may require a browser/session key and can break). Falls back to an [organization admin key](https://platform.openai.com) with `api.usage.read` for org spend against a monthly budget. Empty API balance shows as 100% used. This is separate from ChatGPT/Codex subscription limits.

## Claude

**Claude.ai** plan rate limits (OAuth / Claude Code login) are separate from **Claude API** Console spend.

## Gemini App (limits)

Sign in to **Antigravity IDE** on this machine, or run **`gemini login`** with the [Gemini CLI](https://github.com/google-gemini/gemini-cli) (`npm i -g @google/gemini-cli`). Connect tries Gemini CLI first when installed, otherwise launches Antigravity IDE. The widget reads your local OAuth session and shows grouped **Gemini Models** and **Claude and GPT models** 5-hour and weekly limits (Antigravity), or per-model Gemini CLI quotas as a fallback. No API keys or project IDs needed. Gemini Developer API billing is not metered yet. Uses undocumented Google Cloud Code endpoints and may change without notice.

## fal.ai (credits)

Paste an **Admin** API key from [fal.ai/dashboard/keys](https://fal.ai/dashboard/keys). The widget calls `GET /v1/account/billing?expand=credits` and shows **remaining balance** (fal does not expose total purchased / lifetime used). The bar uses a low-balance heuristic (same idea as OpenCode Zen): empty when balance is `$0`, then rising pressure as remaining drops through `$10` / `$5` / `$1`. Quota alerts can fire when that heuristic reaches your unused-quota threshold.

## Settings location

| Platform | Path |
|----------|------|
| Windows | `%LOCALAPPDATA%\deez-fuel-gauge\settings.json` |
| macOS | `~/Library/Application Support/deez-fuel-gauge/settings.json` |

Encrypted API keys: `credentials/` in the same folder.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database:
   - **Windows:** `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
   - **macOS:** `~/Library/Application Support/Cursor/User/globalStorage/state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans), or falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.
3. Optionally fetches **Grok Bot** weekly usage from `GetSandUsageStatus` with the same Cursor token.
4. Optionally enriches OpenAI / Gemini bars from Cursor's aggregated usage events.
5. Optionally fetches **Codex / ChatGPT** 5-hour and weekly limits from `chatgpt.com` when Codex auth or a session cookie is available.
6. Optionally fetches **Gemini App** grouped Gemini and third-party 5-hour and weekly limits from Google Cloud Code when Antigravity IDE or Gemini CLI is signed in locally.
7. Optionally fetches **OpenAI API** prepaid credits (`credit_grants`, best-effort) or Admin Platform spend vs budget when configured in settings.
8. Optionally fetches **fal.ai** prepaid credit balance via the official Platform billing API when an Admin key is saved.

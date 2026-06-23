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

- **Windows:** if .NET 8 is missing, the launcher can install it via winget.
- **macOS:** if .NET 8 is missing, the launcher opens the official download page in your browser.
- **macOS:** if macOS blocks the launcher the first time, right-click **`setup-and-run.app`** and choose **Open**.

## Usage

- **Drag** the pill anywhere on screen.
- **Right-click** for Refresh or Quit.
- Position is saved locally (see paths below).
- Usage refreshes automatically every 5 minutes.

### Settings location

| Platform | Path |
|----------|------|
| Windows | `%LOCALAPPDATA%\cursor-usage-widget\settings.json` |
| macOS | `~/Library/Application Support/cursor-usage-widget/settings.json` |

## Optional: run at login

**Windows**

1. Build the widget once using **`setup-and-run.bat`**.
2. Press `Win+R`, type `shell:startup`, press Enter.
3. Create a shortcut to `CursorUsageWidget\bin\Release\net8.0\CursorUsageWidget.exe` in that folder.

**macOS**

1. Build the widget once using **`setup-and-run.app`**.
2. Open **System Settings → General → Login Items**.
3. Add `CursorUsageWidget/bin/Release/net8.0/CursorUsageWidget`.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database:
   - **Windows:** `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
   - **macOS:** `~/Library/Application Support/Cursor/User/globalStorage/state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans).
3. Falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.

## Disclaimer

- Uses **undocumented** Cursor endpoints that may change or break without notice.
- Not affiliated with or endorsed by Cursor.
- Your access token is read locally and sent only to `api2.cursor.sh` over HTTPS. It is never stored by this widget.

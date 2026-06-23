# Cursor Usage Widget

A lightweight, draggable desktop overlay that shows how much of your Cursor included usage cap you've used.

Runs on **Windows 10/11** and **macOS**.

## Requirements

- Windows 10/11 or macOS 12+
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Cursor IDE logged in on the same user profile

## One-click setup & run

**Windows:** double-click **`setup-and-run.bat`** in this folder.

**macOS:** in Terminal, run:

```bash
chmod +x setup-and-run.sh
./setup-and-run.sh
```

On first run the script builds the widget and launches it. Later runs rebuild and start the widget.

On Windows, `setup-and-run.bat` can install the .NET 8 SDK via winget if needed.

## Build & run (manual)

```bash
cd cursor-usage-widget
dotnet build
dotnet run --project CursorUsageWidget
```

## Publish (single executable)

**Windows:**

```powershell
dotnet publish CursorUsageWidget -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output: `CursorUsageWidget/bin/Release/net8.0/win-x64/publish/CursorUsageWidget.exe`

**macOS (Apple Silicon):**

```bash
dotnet publish CursorUsageWidget -c Release -r osx-arm64 --self-contained false -p:PublishSingleFile=true
```

Output: `CursorUsageWidget/bin/Release/net8.0/osx-arm64/publish/CursorUsageWidget`

**macOS (Intel):** use `-r osx-x64` instead of `osx-arm64`.

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

1. Publish or build the executable.
2. Press `Win+R`, type `shell:startup`, press Enter.
3. Create a shortcut to `CursorUsageWidget.exe` in that folder.

**macOS**

1. Publish or build the app.
2. Open **System Settings → General → Login Items**.
3. Add `CursorUsageWidget` (or a shell script that runs it).

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

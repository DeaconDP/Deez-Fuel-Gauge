# Cursor Usage Widget

A lightweight, draggable Windows overlay that shows how much of your Cursor included usage cap you've used.

## Install

Download **`cursor-usage-widget-win-Setup.exe`** from the [latest release](https://github.com/USER/cursor-usage-widget/releases/latest) and run it.

The installer is also committed in [`Releases/`](Releases/) (tracked with Git LFS). If you clone the repo to get the installer directly, install [Git LFS](https://git-lfs.com/) first:

```powershell
git lfs install
git clone https://github.com/USER/cursor-usage-widget.git
```

## Requirements

- Windows 10/11
- Cursor IDE logged in on the same Windows profile

Developers also need the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

## Build & run

```powershell
dotnet build
dotnet run --project CursorUsageWidget
```

## Build installer (Setup.exe)

```powershell
.\scripts\build-release.ps1
```

This publishes a self-contained app and packages it with [Velopack](https://docs.velopack.io/) into `Releases/`:

- `cursor-usage-widget-win-Setup.exe` — Windows installer
- `cursor-usage-widget-1.0.0-full.nupkg` — full update package
- `cursor-usage-widget-win-Portable.zip` — portable build

Pass an explicit version if needed:

```powershell
.\scripts\build-release.ps1 -Version 1.0.1
```

## Usage

- **Drag** the pill anywhere on screen.
- **Right-click** for Refresh or Quit.
- Position is saved to `%LOCALAPPDATA%\cursor-usage-widget\settings.json`.
- Usage refreshes automatically every 5 minutes.

## Optional: run at Windows startup

1. Install via Setup.exe (or build the app).
2. Press `Win+R`, type `shell:startup`, press Enter.
3. Create a shortcut to `CursorUsageWidget.exe` in that folder.

## How it works

1. Reads `cursorAuth/accessToken` from Cursor's local SQLite database at  
   `%APPDATA%\Cursor\User\globalStorage\state.vscdb`
2. Calls Cursor's unofficial `GetCurrentPeriodUsage` API (Pro/Ultra/Team plans).
3. Falls back to `GET /auth/usage` for legacy Enterprise request-based quotas.

## Disclaimer

- Uses **undocumented** Cursor endpoints that may change or break without notice.
- Not affiliated with or endorsed by Cursor.
- Your access token is read locally and sent only to `api2.cursor.sh` over HTTPS. It is never stored by this widget.

## License

MIT — see [LICENSE](LICENSE).

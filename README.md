# Cursor Usage Widget

A lightweight, draggable Windows overlay that shows how much of your Cursor included usage cap you've used.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Cursor IDE logged in on the same Windows profile

## One-click setup & run

Double-click **`setup-and-run.bat`** in this folder.

On first run it will install the .NET 8 SDK via winget if needed, build the widget, and launch it. Later runs rebuild and start the widget.

## Build & run (manual)

```powershell
cd C:\Projects\Cursor\cursor-usage-widget
dotnet build
dotnet run --project CursorUsageWidget
```

## Publish (single exe)

```powershell
dotnet publish CursorUsageWidget -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

The executable will be at `CursorUsageWidget\bin\Release\net8.0-windows\win-x64\publish\CursorUsageWidget.exe`.

## Usage

- **Drag** the pill anywhere on screen.
- **Right-click** for Refresh or Quit.
- Position is saved to `%LOCALAPPDATA%\cursor-usage-widget\settings.json`.
- Usage refreshes automatically every 5 minutes.

## Optional: run at Windows startup

1. Publish or build the exe.
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

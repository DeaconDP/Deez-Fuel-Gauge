@echo off
setlocal
cd /d "%~dp0"

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-and-run.ps1"
if errorlevel 1 (
    echo.
    echo Setup failed. See the message above.
    pause
)

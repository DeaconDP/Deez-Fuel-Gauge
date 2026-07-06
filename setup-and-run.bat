@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
rem setup-and-run.ps1 self-updates this checkout, which may replace this very
rem file while it runs. cmd re-reads batch files from disk between lines, so
rem everything after the PowerShell call must stay on that same single line
rem (!errorlevel! needs delayed expansion to be read after PowerShell exits).
rem Failure messages and the pause live inside setup-and-run.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0setup-and-run.ps1" & exit /b !errorlevel!

@echo off
setlocal enabledelayedexpansion
cd /d "%~dp0"
rem Failure messages and the pause live inside run.ps1.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0run.ps1" & exit /b !errorlevel!

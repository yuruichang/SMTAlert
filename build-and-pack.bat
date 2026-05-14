@echo off
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-and-pack.ps1"
if errorlevel 1 (
    echo Script execution failed
    pause
)

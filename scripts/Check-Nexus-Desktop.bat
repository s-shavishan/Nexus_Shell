@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0desktop-health.ps1"
if errorlevel 1 pause

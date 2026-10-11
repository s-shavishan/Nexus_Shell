@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0verify-native-ui.ps1" -PublishDirectory "%~dp0." -ReportFile "%LOCALAPPDATA%\WhiteDreams\NexusShell\diagnostics\native-ui.json"
if errorlevel 1 pause

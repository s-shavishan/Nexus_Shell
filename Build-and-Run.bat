@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1" -Run
if errorlevel 1 (
  echo.
  echo Build failed. Read the message above and START-HERE.md.
  pause
)
endlocal

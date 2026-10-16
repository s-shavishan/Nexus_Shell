@REM නමෝ බුද්ධාය 🤍
@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build\build.ps1" -Run
if errorlevel 1 (
  echo.
  echo Build failed. Read the message above and docs\DEVELOPMENT.md.
  pause
)
endlocal

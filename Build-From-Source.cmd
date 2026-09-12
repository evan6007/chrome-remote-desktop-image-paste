@echo off
setlocal
cd /d "%~dp0"
echo Developer source build. Most users should download the release installer.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Setup.ps1"
if errorlevel 1 (
  echo Build failed. Read the error above.
  pause
  exit /b 1
)

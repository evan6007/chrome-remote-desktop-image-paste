@echo off
setlocal
cd /d "%~dp0"
title Chrome Remote Desktop Image Paste - Install Receiver
echo Run this on the REMOTE computer you want to paste images into.
echo Keep this entire extracted project folder for step 2 and future updates.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Setup.ps1" -Action Receiver
if errorlevel 1 (
  echo.
  echo Installation did not finish. Read the error above and the troubleshooting guide.
  pause
  exit /b 1
)
echo.
pause

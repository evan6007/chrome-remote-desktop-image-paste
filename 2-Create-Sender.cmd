@echo off
setlocal
cd /d "%~dp0"
title Chrome Remote Desktop Image Paste - Create Sender
echo Run this on the SAME remote computer and in the SAME project folder as step 1.
echo First wait for the receiver window to say that the connection is verified.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Setup.ps1" -Action Sender
if errorlevel 1 (
  echo.
  echo Sender creation did not finish. Read the error above and the troubleshooting guide.
  pause
  exit /b 1
)
echo.
pause

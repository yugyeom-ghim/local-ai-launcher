@echo off
setlocal
powershell.exe -NoProfile -File "%~dp0Verify-Package.ps1"
if errorlevel 1 (
  echo Verification failed or policy prevented the script from running.
  echo Keep the company policy in place. Ask the approved packaging administrator.
  pause
  exit /b 1
)
pause
exit /b 0

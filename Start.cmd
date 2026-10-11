@echo off
setlocal
cd /d "%~dp0"
if not exist "SI.LocalAI.Launcher.exe" (
  echo Launcher EXE is missing. Restore the complete release package.
  echo This employee entry point does not compile source code.
  pause
  exit /b 1
)
start "" "%~dp0SI.LocalAI.Launcher.exe" %*
if errorlevel 1 (
  echo Launch failed. Check your approved .NET Framework and application policy.
  pause
  exit /b 1
)
exit /b 0

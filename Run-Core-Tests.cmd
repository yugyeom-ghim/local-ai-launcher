@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Build.cmd" /test
if errorlevel 1 (
  pause
  exit /b 1
)
"state\CoreTests.exe" "state\core-test-results.txt"
set "RESULT=%ERRORLEVEL%"
echo.
echo Test report: state\core-test-results.txt
pause
exit /b %RESULT%

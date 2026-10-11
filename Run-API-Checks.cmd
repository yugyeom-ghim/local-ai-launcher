@echo off
setlocal
cd /d "%~dp0"
call Build.cmd /apitest
if errorlevel 1 exit /b 1
"state\ApiChecks.exe" "%~dp0state"
set "RESULT=%ERRORLEVEL%"
echo These are API logic checks, not live Windows inference tests.
pause
exit /b %RESULT%

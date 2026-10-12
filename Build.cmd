@echo off
setlocal
cd /d "%~dp0"
set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319"
if not exist "%FRAMEWORK%\csc.exe" set "FRAMEWORK=%WINDIR%\Microsoft.NET\Framework\v4.0.30319"
set "CSC=%FRAMEWORK%\csc.exe"
if not exist "%CSC%" (
  echo ERROR: The .NET Framework C# compiler was not found.
  echo Ask IT to approve .NET Framework 4.8, or build the included solution in Visual Studio.
  exit /b 1
)
set "REFS="
set "MISSING="
for %%A in (System System.Core System.Drawing System.Windows.Forms System.Net.Http System.Web.Extensions System.Management) do call :AddReference %%A
if defined MISSING exit /b 1
if not exist "state" mkdir "state" 2>nul
set "BUILDLOG=%TEMP%\SI-LocalAI-build-%RANDOM%.log"
if /i "%~1"=="/test" goto :BuildTests
if /i "%~1"=="/apitest" goto :BuildApiTests
if not exist "state\build" mkdir "state\build"
echo Building Local AI Launcher from the included source...
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:SILocalAI.Program /win32manifest:"src\app.manifest" /resource:"src\Resources\si-logo.png",SILocalAI.BrandLogo /resource:"src\Resources\ModelCatalog.json",SILocalAI.ModelCatalog /out:"state\build\LocalAI.Launcher.exe" %REFS% "src\AssemblyInfo.cs" "src\Core.cs" "src\Library.cs" "src\ModelPacks.cs" "src\WindowsHost.cs" "src\Theme.cs" "src\Layout.cs" "src\Dashboard.cs" "src\ApiIntegration.cs" "src\Connections.cs" "src\App.cs" > "%BUILDLOG%" 2>&1
if errorlevel 1 goto :Failed
move /y "state\build\LocalAI.Launcher.exe" "LocalAI.Launcher.exe" >nul
if errorlevel 1 (
  echo Cannot replace the launcher. Close Local AI Launcher, then run Build.cmd again.
  exit /b 1
)
copy /y "src\LocalAI.Launcher.exe.config" "LocalAI.Launcher.exe.config" >nul
if exist "state" copy /y "%BUILDLOG%" "state\build.log" >nul 2>nul
> ".launcher-build-version.txt" echo 1.6.2
echo Build complete: LocalAI.Launcher.exe
exit /b 0

:BuildTests
"%CSC%" /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:SILocalAI.TestProgram /resource:"src\Resources\ModelCatalog.json",SILocalAI.ModelCatalog /out:"state\CoreTests.exe" %REFS% "src\Core.cs" "src\Library.cs" "src\ModelPacks.cs" "src\ApiIntegration.cs" "tests\CoreTests.cs" > "%BUILDLOG%" 2>&1
if errorlevel 1 goto :Failed
exit /b 0

:BuildApiTests
"%CSC%" /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /out:"state\ApiChecks.exe" %REFS% /r:"LocalAI.Launcher.exe" "tests\ApiChecks.cs" > "%BUILDLOG%" 2>&1
if errorlevel 1 goto :Failed
copy /y "LocalAI.Launcher.exe" "state\LocalAI.Launcher.exe" >nul
if errorlevel 1 goto :Failed
exit /b 0

:Failed
type "%BUILDLOG%"
echo.
echo Build failed. Log: %BUILDLOG%
echo No execution policy or security settings were changed.
exit /b 1

:AddReference
set "REFPATH="
if exist "%FRAMEWORK%\%~1.dll" set "REFPATH=%FRAMEWORK%\%~1.dll"
if not defined REFPATH for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_MSIL\%~1\*") do if exist "%%~fD\%~1.dll" set "REFPATH=%%~fD\%~1.dll"
if not defined REFPATH for /d %%D in ("%WINDIR%\Microsoft.NET\assembly\GAC_64\%~1\*") do if exist "%%~fD\%~1.dll" set "REFPATH=%%~fD\%~1.dll"
if not defined REFPATH (
  echo ERROR: Required .NET Framework assembly not found: %~1.dll
  set "MISSING=1"
  exit /b 1
)
set REFS=%REFS% /r:"%REFPATH%"
exit /b 0

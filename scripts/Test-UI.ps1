param([switch]$Baseline, [string]$Output = 'state/ui-checks', [string]$PackageRoot = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
New-Item $Output -ItemType Directory -Force | Out-Null
& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 '/out:UiChecks.exe' /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll '/r:LocalAI.Launcher.exe' 'tests\UiChecks.cs'
if ($LASTEXITCODE -ne 0) { throw 'UI probe compilation failed' }
$arguments = @('"' + (Join-Path $repoRoot $Output) + '"', $(if ($Baseline) {'baseline'} else {'current'}))
if ($PackageRoot) { $arguments += '"' + $PackageRoot + '"' }
$p = Start-Process ./UiChecks.exe -ArgumentList $arguments -PassThru -RedirectStandardOutput "$Output/stdout.txt" -RedirectStandardError "$Output/stderr.txt"
if (-not $p.WaitForExit(90000)) { Stop-Process -Id $p.Id -Force; if ($Baseline) { 'Baseline UI exceeded 90s' | Set-Content "$Output/TIMEOUT.txt"; exit 0 }; throw 'UI probe timed out' }
$p.Refresh()
Get-Content "$Output/stdout.txt"
if (Test-Path "$Output/stderr.txt") { Get-Content "$Output/stderr.txt" }
if ($p.ExitCode -ne 0 -and -not $Baseline) { throw "UI probe failed: $($p.ExitCode)" }

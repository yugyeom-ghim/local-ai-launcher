$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path -Parent $PSScriptRoot)
$lock = Get-Content scripts/ASSET_LOCK.json -Raw | ConvertFrom-Json
$asset = @($lock.assets | Where-Object { $_.target -eq 'offline-assets/ai/bin/win-cpu-x64' })[0]
$root = Join-Path $env:RUNNER_TEMP 'local-ai-cors-runtime'
New-Item $root -ItemType Directory -Force | Out-Null
$zip = Join-Path $root 'runtime.zip'
Invoke-WebRequest $asset.url -OutFile $zip
if ((Get-Item $zip).Length -ne $asset.size -or (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant() -ne $asset.sha256) { throw 'CORS test runtime hash mismatch' }
Expand-Archive $zip -DestinationPath $root -Force
$server = (Get-ChildItem $root -Filter llama-server.exe -Recurse | Select-Object -First 1).FullName
$csc = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $csc /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output '/out:state\CorsChecks.exe' /r:System.dll /r:System.Core.dll '/r:LocalAI.Launcher.exe' 'tests\CorsChecks.cs'
if ($LASTEXITCODE -ne 0) { throw 'CORS test compilation failed' }
Copy-Item LocalAI.Launcher.exe state/LocalAI.Launcher.exe -Force
$report = & ./state/CorsChecks.exe $server 2>&1
$result = $LASTEXITCODE
$report | Set-Content state/windows-cors-checks.txt -Encoding utf8
$report | Write-Host
if ($result -ne 0) { throw 'Pinned Windows server CORS checks failed' }

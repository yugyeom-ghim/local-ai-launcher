param([Parameter(Mandatory=$true)][string]$PayloadRoot, [Parameter(Mandatory=$true)][string]$OutDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
$version = (Get-Content (Join-Path $repoRoot 'release-version.txt') -Raw).Trim()
$compilerRoot = Join-Path $env:RUNNER_TEMP 'inno-setup-6.7.3'
$compilerSetup = Join-Path $env:RUNNER_TEMP 'innosetup-6.7.3.exe'
$compilerUrl = 'https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe'
$compilerHash = '9c73c3bae7ed48d44112a0f48e66742c00090bdb5bef71d9d3c056c66e97b732'
Invoke-WebRequest $compilerUrl -OutFile $compilerSetup
if ((Get-Item $compilerSetup).Length -ne 10592232 -or (Get-FileHash $compilerSetup -Algorithm SHA256).Hash.ToLowerInvariant() -ne $compilerHash) {
    throw 'Inno Setup compiler download hash/size mismatch'
}
# This compiler is installed only on the build runner, not on employee machines.
$process = Start-Process $compilerSetup -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/DIR=`"$compilerRoot`"") -Wait -PassThru
if ($process.ExitCode -ne 0) { throw "Compiler installation failed: $($process.ExitCode)" }
$iscc = Join-Path $compilerRoot 'ISCC.exe'
& $iscc "/DPayloadRoot=$PayloadRoot" "/DOutputRoot=$OutDirectory" "/DReleaseVersion=$version" (Join-Path $repoRoot 'installer/Local-AI.iss')
if ($LASTEXITCODE -ne 0) { throw 'Offline installer compilation failed' }

$setup = Join-Path $OutDirectory 'Local_AI_Offline_Setup.exe'
$installerFiles = @(Get-Item $setup) + @(Get-ChildItem $OutDirectory -Filter 'Local_AI_Offline_Setup-*.bin' -File | Sort-Object Name)
if ($installerFiles.Count -lt 2) { throw 'Installer data slices are missing' }
$baseUrl = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/"
$manifest = @{ version = $version; files = @($installerFiles | ForEach-Object {
    if ($_.Length -ge 2GB) { throw 'Installer asset exceeds GitHub size limit' }
    @{ name=$_.Name; size=$_.Length; sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); url=$baseUrl+$_.Name }
}) }
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $OutDirectory 'installer-download-manifest.json') -Encoding utf8
$compilerRecord = @{ name='Inno Setup'; version='6.7.3'; url=$compilerUrl; size=10592232; sha256=$compilerHash; license='Inno Setup License'; source_commit='4adf37ed7f3fd2bd11c6836ba056e3de170fbabf' }
$compilerRecord | ConvertTo-Json | Set-Content (Join-Path $OutDirectory 'INSTALLER_BUILD_TOOL.json') -Encoding utf8
& (Join-Path $repoRoot 'scripts/Test-Offline-Installer.ps1') -SetupPath $setup -PayloadRoot $PayloadRoot -OutDirectory $OutDirectory
if (-not $?) { throw 'Installer validation failed' }

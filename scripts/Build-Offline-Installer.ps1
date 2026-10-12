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
$singleRoot = Join-Path $env:RUNNER_TEMP ("single-installer-$version")
New-Item $singleRoot -ItemType Directory -Force | Out-Null
& $iscc "/DPayloadRoot=$PayloadRoot" "/DOutputRoot=$singleRoot" "/DReleaseVersion=$version" (Join-Path $repoRoot 'installer/Local-AI.iss')
if ($LASTEXITCODE -ne 0) { throw 'Offline installer compilation failed' }

$setup = Join-Path $singleRoot 'Local_AI_Offline_Setup.exe'
if (@(Get-ChildItem $singleRoot -File).Count -ne 1 -or -not (Test-Path $setup)) { throw 'Standalone installer must contain exactly one EXE' }
if ((Get-Item $setup).Length -ge 4294967295L) { throw 'Installer exceeds Windows executable size limit' }
$baseUrl = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/"
& python (Join-Path $repoRoot 'scripts/split_installer.py') --installer $setup --output $OutDirectory --base-url $baseUrl --version $version
if ($LASTEXITCODE -ne 0) { throw 'Installer transport split failed' }
# The release contains transport parts. The downloader must reconstruct and verify the EXE before testing it.
Remove-Item $setup
$compilerRecord = @{ name='Inno Setup'; version='6.7.3'; url=$compilerUrl; size=10592232; sha256=$compilerHash; license='Inno Setup License'; source_commit='4adf37ed7f3fd2bd11c6836ba056e3de170fbabf' }
$compilerRecord | ConvertTo-Json | Set-Content (Join-Path $OutDirectory 'INSTALLER_BUILD_TOOL.json') -Encoding utf8

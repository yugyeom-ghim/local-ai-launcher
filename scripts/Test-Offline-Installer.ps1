param([Parameter(Mandatory=$true)][string]$SetupPath, [Parameter(Mandatory=$true)][string]$PayloadRoot, [Parameter(Mandatory=$true)][string]$OutDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$installPath = [IO.Path]::GetFullPath((Join-Path $env:RUNNER_TEMP '설치 검증/SI Local AI'))
$results = [Collections.Generic.List[string]]::new()
function Check([bool]$condition, [string]$name) {
    if (-not $condition) { throw "Installer check failed: $name" }
    $results.Add("PASS $name"); Write-Host "PASS $name"
}
function Run-Setup([string]$name) {
    $logPath = Join-Path $OutDirectory "$name.log"
    $args = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/DIR=`"$installPath`"",'/TASKS=desktopicon',"/LOG=`"$logPath`"")
    $process = Start-Process $SetupPath -ArgumentList $args -Wait -PassThru
    return $process.ExitCode
}

# The mutex name matches the Launcher, including UTF-8 Korean paths and final slash.
$bytes = [Text.Encoding]::UTF8.GetBytes(($installPath.TrimEnd('\')+'\').ToLowerInvariant())
$sha = [Security.Cryptography.SHA256]::Create()
$mutexHash = [BitConverter]::ToString($sha.ComputeHash($bytes)).Replace('-','').ToLowerInvariant()
$sha.Dispose()
$mutex = [Threading.Mutex]::new($true, ('Local\SI.LocalAI.'+$mutexHash.Substring(0,24)))
try { Check ((Run-Setup 'installer-running-guard') -ne 0) 'running Launcher blocks installation without being terminated' }
finally { $mutex.ReleaseMutex(); $mutex.Dispose() }

Check ((Run-Setup 'installer-install') -eq 0) 'native Windows unattended installation succeeds'
Check (Test-Path (Join-Path $installPath 'SI.LocalAI.Launcher.exe')) 'Launcher EXE installed'
Check (Test-Path (Join-Path $installPath 'installer/assets/satreci.ico')) 'Satrec Initiative icon installed'
$count = 0
foreach ($line in [IO.File]::ReadLines((Join-Path $PayloadRoot 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw 'Invalid payload SHA256 manifest' }
    $expected = $Matches[1]; $relative = $Matches[2]
    $path = Join-Path $installPath $relative
    if (-not (Test-Path $path -PathType Leaf) -or (Get-FileHash $path -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) {
        throw "Installed file hash mismatch: $relative"
    }
    $count++
}
Check ($count -gt 187) "all $count installed payload files match SHA256"
$menu = [IO.Path]::GetFullPath((Join-Path ([Environment]::GetFolderPath('Programs')) 'SI Local AI/SI Local AI Launcher.lnk'))
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) 'SI Local AI Launcher.lnk'
Check (Test-Path $menu) 'Start menu shortcut created'
Check (Test-Path $desktop) 'optional desktop shortcut created'
$shell = New-Object -ComObject WScript.Shell
$shortcutTarget = $shell.CreateShortcut($menu).TargetPath
$expectedTarget = [IO.Path]::GetFullPath((Join-Path $installPath 'SI.LocalAI.Launcher.exe'))
Write-Host "Shortcut target: $shortcutTarget; expected: $expectedTarget"
Check (-not [String]::IsNullOrWhiteSpace($shortcutTarget) -and [IO.Path]::GetFullPath($shortcutTarget) -eq $expectedTarget) 'shortcut targets the installed Launcher'

$state = Join-Path $installPath 'state'; New-Item $state -ItemType Directory -Force | Out-Null
$settings = Join-Path $state 'settings.json'; $key = Join-Path $state 'local-api-key.txt'
$personal = Join-Path $installPath 'offline-assets/ai/models/Personal-Test-Model.gguf'
[IO.File]::WriteAllText($settings, '{"keep":"existing-settings"}')
[IO.File]::WriteAllText($key, 'installer-test-sentinel-not-a-real-key')
[IO.File]::WriteAllText($personal, 'user-added-file-preservation-fixture')
$settingsHash = (Get-FileHash $settings).Hash; $keyHash = (Get-FileHash $key).Hash; $personalHash = (Get-FileHash $personal).Hash
Check ((Run-Setup 'installer-reinstall') -eq 0) 'reinstallation succeeds at the existing location'
Check ((Get-FileHash $settings).Hash -eq $settingsHash -and (Get-FileHash $key).Hash -eq $keyHash -and (Get-FileHash $personal).Hash -eq $personalHash) 'reinstallation preserves existing settings, local key and added files'

$uninstaller = Join-Path $installPath 'unins000.exe'
$process = Start-Process $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',"/LOG=`"$(Join-Path $OutDirectory 'installer-uninstall.log')`"") -Wait -PassThru
Check ($process.ExitCode -eq 0) 'native Windows uninstall succeeds'
Check (-not (Test-Path (Join-Path $installPath 'SI.LocalAI.Launcher.exe'))) 'uninstall removes the Launcher'
Check (-not (Test-Path (Join-Path $installPath 'offline-assets/ai/models/Qwen3.5-4B-Q4_K_M.gguf'))) 'uninstall removes the bundled model'
Check ((Get-FileHash $settings).Hash -eq $settingsHash -and (Get-FileHash $key).Hash -eq $keyHash -and (Get-FileHash $personal).Hash -eq $personalHash) 'uninstall preserves personal settings, local key and user-added files'
Check (-not (Test-Path $menu) -and -not (Test-Path $desktop)) 'uninstall removes created shortcuts'

$receipt = @"
# Offline installer validation

Run: https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID
Commit: $env:GITHUB_SHA
Windows Server 2022: real setup/reinstall/uninstall executed in a temporary Korean path.

$($results -join "`n")

- Installer payload contains the actual Qwen model, CPU and both CUDA runtimes. No model inference executed.
- Setup script has no network/download operation, firewall change or execution-policy change.
- This test did not disconnect the runner's network and does not claim packet-capture verification.
- Interactive wizard/DPI, Windows desktop model inference and company OpenCode/MCP remain unverified.
- Installer/compiler and Launcher binaries are not company-code-signed; existing execution policy applies.
"@
$receipt | Set-Content (Join-Path $OutDirectory 'INSTALLER_VALIDATION.md') -Encoding utf8
# Retain concise evidence in the release; CI paths and file-by-file logs stay on the runner.
Get-ChildItem $OutDirectory -Filter 'installer-*.log' -File | Remove-Item

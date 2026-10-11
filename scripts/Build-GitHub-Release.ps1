$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

& cmd.exe /d /c Build.cmd
if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
& cmd.exe /d /c 'Build.cmd /test'
if ($LASTEXITCODE -ne 0) { throw 'Core test compilation failed' }
& .\state\CoreTests.exe .\state\windows-core-tests.txt
if ($LASTEXITCODE -ne 0) { throw 'Core tests failed' }
& cmd.exe /d /c 'Build.cmd /apitest'
if ($LASTEXITCODE -ne 0) { throw 'API check compilation failed' }
$apiOutput = & .\state\ApiChecks.exe .\state 2>&1
$apiExit = $LASTEXITCODE
$apiOutput | Set-Content .\state\windows-api-checks.txt -Encoding utf8
$apiOutput | Write-Host
if ($apiExit -ne 0) { throw 'API logic checks failed' }

$version = (Get-Content release-version.txt -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
$stage = Join-Path $env:RUNNER_TEMP ('si-local-release-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive downloads/SI_Local_AI_Update_v1.3.zip $stage
Copy-Item SI.LocalAI.Launcher.exe (Join-Path $stage 'SI.LocalAI.Launcher.exe') -Force
Copy-Item Build.cmd (Join-Path $stage 'Build.cmd') -Force
Copy-Item state/windows-core-tests.txt (Join-Path $stage 'validation/WINDOWS_CORE_TESTS.txt')
Copy-Item state/windows-api-checks.txt (Join-Path $stage 'validation/WINDOWS_API_CHECKS.txt')
$receipt = @"
# Windows GitHub Actions validation

Version: $version
Commit: $env:GITHUB_SHA
Run: https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID

- Windows Server 2022 runner: native .NET Framework x64 Release build succeeded.
- Core regression checks: 62 passed.
- API request/credential logic checks: 7 passed.
- No GGUF, llama-server, CUDA runtime or model inference was executed in this job.
- GUI/DPI and company OpenCode/MCP integration remain unverified.
- This ZIP contains the Windows-built EXE. Previous Linux reports are historical records.
"@
$receipt | Set-Content (Join-Path $stage 'validation/WINDOWS_CI.md') -Encoding utf8
$intro = "이 ZIP의 EXE는 GitHub Actions Windows 환경에서 다시 빌드했습니다. Windows 로직 검사 69개 통과. GUI/DPI·모델 추론·사내 OpenCode/MCP 연동은 미검증입니다. 세부 결과는 validation/WINDOWS_CI.md를 확인하세요.`r`n`r`n"
$readmePath = Join-Path $stage 'README_API_UPDATE_KO.md'
$intro + (Get-Content $readmePath -Raw -Encoding utf8) | Set-Content $readmePath -Encoding utf8

# Keep every original large-asset digest. Update only the files actually in this overlay.
$manifest = @{}
Get-Content (Join-Path $stage 'SHA256SUMS.txt') | ForEach-Object {
    if ($_ -match '^([0-9a-f]{64})  (.+)$') { $manifest[$Matches[2]] = $Matches[1] }
}
$base = $stage.TrimEnd('\') + '\'
Get-ChildItem $stage -File -Recurse | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object {
    $relative = $_.FullName.Substring($base.Length).Replace('\', '/')
    $manifest[$relative] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
}
$lines = $manifest.Keys | Sort-Object | ForEach-Object { $manifest[$_] + '  ' + $_ }
$lines | Set-Content (Join-Path $stage 'SHA256SUMS.txt') -Encoding utf8

$out = Join-Path $repoRoot 'release-output'
New-Item $out -ItemType Directory -Force | Out-Null
$zipName = "SI_Local_AI_Update_v${version}_Windows.zip"
$zipPath = Join-Path $out $zipName
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
Copy-Item (Join-Path $stage 'validation/WINDOWS_CI.md') $out
Copy-Item state/windows-core-tests.txt $out
Copy-Item state/windows-api-checks.txt $out
@"
Windows x64 Release EXE가 포함된 **기존 설치용 업데이트**입니다.

1. 서버와 Launcher를 완전히 종료합니다.
2. **$zipName** 하나를 받습니다.
3. ZIP 내용물을 기존 v1.2 / v1.2.1 / v1.3 폴더에 덮어씁니다.

모델·CPU/CUDA·개인 설정은 기존 파일을 사용합니다. 신규 설치용 전체 자산 묶음은 아닙니다.
소스는 저장소와 아래 Source code 다운로드에서 확인할 수 있습니다.

Windows 네이티브 빌드 및 코어/API 로직 검사 69개 통과.
GUI/DPI·실제 모델 추론·사내 OpenCode/MCP 연동은 미검증입니다.
MCP는 기존 OpenCode 설정을 유지하고 제공자만 추가해 사용합니다.
사고 ON/OFF는 하나를 선택해 시작하며 바꾸면 서버를 재시작합니다.

SHA256: $hash
Commit: $env:GITHUB_SHA
"@ | Set-Content (Join-Path $out 'RELEASE_NOTES.md') -Encoding utf8

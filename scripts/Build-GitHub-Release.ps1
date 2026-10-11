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

$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$web = (Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL/System.Web.Extensions') -Filter System.Web.Extensions.dll -Recurse | Select-Object -First 1).FullName
& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:DownloadTests '/out:state\DownloadTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" 'tools\FullDownload.cs' 'tests\DownloadTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Download test compilation failed' }
$downloadOutput = & .\state\DownloadTests.exe 2>&1
$downloadExit = $LASTEXITCODE
$downloadOutput | Set-Content state/windows-download-checks.txt -Encoding utf8
$downloadOutput | Write-Host
if ($downloadExit -ne 0) { throw 'Download checks failed' }

$version = (Get-Content release-version.txt -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
$stage = Join-Path $env:RUNNER_TEMP ('si-local-release-' + [Guid]::NewGuid().ToString('N'))
Expand-Archive downloads/SI_Local_AI_Update_v1.3.zip $stage
# Always ship the same source and instructions as the freshly compiled EXE.
foreach ($folder in @('src', 'tests', 'tools')) {
    New-Item (Join-Path $stage $folder) -ItemType Directory -Force | Out-Null
    Copy-Item "$folder/*" (Join-Path $stage $folder) -Recurse -Force
}
foreach ($file in @('SI.LocalAI.Launcher.exe', 'SI.LocalAI.Launcher.exe.config', 'Build.cmd', 'Start.cmd', 'release-version.txt', 'START_HERE_KO.txt', 'README_KO.md', 'VALIDATION.md', 'README_API_UPDATE_KO.md', 'README_FULL_DOWNLOAD_KO.md')) {
    Copy-Item $file (Join-Path $stage $file) -Force
}
Copy-Item state/windows-core-tests.txt (Join-Path $stage 'validation/WINDOWS_CORE_TESTS.txt')
Copy-Item state/windows-api-checks.txt (Join-Path $stage 'validation/WINDOWS_API_CHECKS.txt')
Copy-Item state/windows-download-checks.txt (Join-Path $stage 'validation/WINDOWS_DOWNLOAD_CHECKS.txt')
$receipt = @"
# Windows GitHub Actions validation

Version: $version
Commit: $env:GITHUB_SHA
Run: https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID

- Windows Server 2022 runner: native .NET Framework x64 Release build succeeded.
- Core regression checks: 62 passed.
- API request/credential logic checks: 7 passed.
- Download/assembly checks: 9 passed, including HTTP Range resume and ignored-Range restart.
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
Copy-Item state/windows-download-checks.txt $out

& python -m unittest discover -s tests -p test_distribution.py
if ($LASTEXITCODE -ne 0) { throw 'Distribution split checks failed' }
& python scripts/build_full_release.py --overlay $stage --output $out
if ($LASTEXITCODE -ne 0) { throw 'Full offline package preparation failed' }
& $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:SILocalAI.Distribution.FullDownload '/out:release-output\Download-SI-Local-AI.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:release-output\full-download-manifest.json,FullDownloadManifest' 'tools\FullDownload.cs'
if ($LASTEXITCODE -ne 0) { throw 'Full downloader compilation failed' }
Copy-Item src/SI.LocalAI.Launcher.exe.config (Join-Path $out 'Download-SI-Local-AI.exe.config')
Copy-Item README_FULL_DOWNLOAD_KO.md $out
$full = Get-Content (Join-Path $out 'full-download-manifest.json') -Raw | ConvertFrom-Json
$fullSize = [Math]::Round($full.archive.size / 1GB, 2)
$fullHash = $full.archive.sha256
Get-ChildItem $out -File | Where-Object { $_.Name -notin @('SHA256SUMS.txt', 'RELEASE_NOTES.md') } | Sort-Object Name | ForEach-Object {
    (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
} | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
@"
**모델·CPU·CUDA 12.4/13.4 DLL을 포함한 전체 배포본**과 기존 설치용 업데이트입니다.

### 처음 설치하는 경우

1. [Download-SI-Local-AI.exe](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Download-SI-Local-AI.exe)를 빈 폴더에 받아 실행하세요.
2. GitHub에서 전체 파일을 이어받아 **$fullSize GiB ZIP 하나**로 합치고 SHA256을 검사합니다.
3. ZIP을 사내 승인 절차로 옮기고 압축을 완전히 풀어 Launcher EXE를 실행하세요.

외부망 PC에서 모델이나 LM Studio를 실행할 필요가 없습니다. 다운로드 EXE는 모델을 자동 실행하거나 정책을 바꾸지 않습니다.
브라우저로 직접 받을 경우 모든 .zip.part 파일과 다운로드 EXE를 같은 폴더에 넣고 실행하면 됩니다.
GitHub의 파일당 2GiB 제한 때문에 분할 게시하지만 자동 합치기로 전체 ZIP을 얻습니다. 복구 파일은 없습니다.

### 이미 설치한 경우

서버와 Launcher를 완전히 종료하고 **$zipName** 하나를 받아 기존 폴더에 덮어쓰세요. 기존 모델·실행기·개인 설정을 유지합니다.
Source code ZIP은 소스 전용입니다. 대용량 모델은 위 전체 배포 파일에 들어 있습니다.

앱 상단·창 제목·시작 로그에서 버전 숫자를 제거했습니다. 내부 버전과 GitHub 릴리스는 별도로 관리합니다.

Windows 네이티브 빌드, 코어/API 로직 69개, 다운로드/합치기 검사 9개 통과.
고정 버전의 실제 자산 다운로드·SHA256·전체 ZIP CRC 검증 완료. 세부: FULL_PACKAGE_VALIDATION.json, ASSET_MANIFEST.json, ASSET_LOCK.json.
GUI/DPI·실제 모델 추론·사내 OpenCode/MCP 연동은 미검증입니다.
MCP는 기존 OpenCode 설정을 유지하고 제공자만 추가해 사용합니다.
사고 ON/OFF는 하나를 선택해 시작하며 바꾸면 서버를 재시작합니다.

전체 ZIP SHA256: $fullHash
업데이트 ZIP SHA256: $hash
Commit: $env:GITHUB_SHA
"@ | Set-Content (Join-Path $out 'RELEASE_NOTES.md') -Encoding utf8

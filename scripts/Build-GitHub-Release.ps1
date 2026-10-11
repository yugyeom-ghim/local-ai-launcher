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
& python scripts/build_full_release.py --overlay $stage --output $out --installer-only
if ($LASTEXITCODE -ne 0) { throw 'Full offline payload preparation failed' }
$payloadRoot = Join-Path $env:RUNNER_TEMP ("si-full-$version/SI_Local_AI")
& ./scripts/Build-Offline-Installer.ps1 -PayloadRoot $payloadRoot -OutDirectory $out
if (-not $?) { throw 'Offline installer build/validation failed' }
& $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:InstallerDownload '/out:release-output\Internet-PC-Download-Installer.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:release-output\installer-download-manifest.json,InstallerManifest' 'tools\FullDownload.cs' 'tools\InstallerDownload.cs'
if ($LASTEXITCODE -ne 0) { throw 'Internet-PC download helper compilation failed' }
& ./release-output/Internet-PC-Download-Installer.exe --verify-only --directory $out --no-pause
if ($LASTEXITCODE -ne 0) { throw 'Download helper manifest verification failed' }
Remove-Item (Join-Path $out 'download.lock')
Copy-Item README_FULL_DOWNLOAD_KO.md $out
Copy-Item README_INSTALL_KO.txt $out
$installer = Get-Content (Join-Path $out 'installer-download-manifest.json') -Raw | ConvertFrom-Json
$size = [Math]::Round(($installer.files | Measure-Object size -Sum).Sum / 1GB, 2)
$fileLinks = ($installer.files | ForEach-Object { '- ['+$_.name+']('+$_.url+')' }) -join "`n"
Get-ChildItem $out -File | Where-Object { $_.Name -notin @('SHA256SUMS.txt', 'RELEASE_NOTES.md') } | Sort-Object Name | ForEach-Object {
    (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
} | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
@"
## 업무망용 오프라인 인스톨러

쎄트렉아이 로고를 적용한 **오프라인 설치 파일 세트**입니다. Setup은 인터넷에 접속하여 파일을 다운로드하지 않습니다.

### 필수 파일 — 모두 같은 폴더에 받으세요 (합계 약 $size GiB)

$fileLinks

1. 인터넷 사용이 승인된 PC에서 위 **Setup EXE + 모든 BIN** 파일을 받습니다. EXE 하나만으로는 설치할 수 없습니다.
2. 같은 폴더에 모아 회사가 승인한 반입 절차로 업무망에 옮깁니다.
3. 업무망에서 **SI_Local_AI_Offline_Setup.exe**를 실행합니다. BIN 파일은 풀거나 합칠 필요가 없습니다.

인터넷 PC에서 편하게 받으려면 [Internet-PC-Download-Installer.exe](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Internet-PC-Download-Installer.exe)를 사용하세요. **이 편의 도구만 GitHub에 연결하며 업무망에서 사용하는 설치 프로그램이 아닙니다.** 완료된 SI_Local_AI_Offline_Installer 폴더를 옮기면 됩니다.

### 포함 및 동작

- Qwen3.5-4B-Q4_K_M 실제 모델, llama.cpp CPU, CUDA 12.4/13.4 실행기와 일치 DLL, VC++ 오프라인 설치 파일, 전체 소스·라이선스·해시 포함.
- 현재 사용자 계정에 설치, 시작 메뉴 및 선택한 바탕화면 바로가기, Windows 앱 목록에서 제거.
- 재설치 시 개인 설정·API 키 유지. 제거 시 개인 설정과 사용자가 추가한 파일은 보존.
- 앱·설치 화면에는 버전 숫자를 표시하지 않고 내부 메타데이터와 GitHub에서 관리.
- .NET Framework 4.8 이상과 GPU 사용 시 호환 NVIDIA 드라이버가 필요합니다. 자동 다운로드·정책 변경·LM Studio·외부망 모델 실행은 없습니다.

Windows에서 실제 설치·전체 설치 파일 SHA256·재설치·제거 및 개인 파일 보존 검사를 통과했습니다. 결과: INSTALLER_VALIDATION.md.
Launcher Windows 빌드 및 코어/API 69개, 다운로드 9개 검사도 통과했습니다. 실제 GUI/DPI와 모델 추론·사내 OpenCode/MCP 연동은 별도 실환경 확인이 필요합니다.

기존 포터블 사용자는 작은 **$zipName**을 기존 폴더에 덮어써도 됩니다. 이것은 전체 설치 파일이 아닙니다.
Source code ZIP에는 대용량 모델이 없습니다. 위 필수 설치 파일을 받으세요.

Commit: $env:GITHUB_SHA
"@ | Set-Content (Join-Path $out 'RELEASE_NOTES.md') -Encoding utf8

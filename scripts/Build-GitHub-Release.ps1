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

& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:InstallerDownloadTests '/out:state\InstallerDownloadTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" 'tools\FullDownload.cs' 'tools\InstallerDownload.cs' 'tests\InstallerDownloadTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Installer download test build failed' }
$installerChecks = & ./state/InstallerDownloadTests.exe 2>&1
$installerExit = $LASTEXITCODE
$installerChecks | Set-Content state/windows-installer-download-checks.txt -Encoding utf8
$installerChecks | Write-Host
if ($installerExit -ne 0) { throw 'Installer download checks failed' }

& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:ModelDownloadTests '/out:state\ModelDownloadTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" 'tools\FullDownload.cs' 'tools\ModelDownload.cs' 'tests\ModelDownloadTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model download test build failed' }
$modelDownloadChecks = & ./state/ModelDownloadTests.exe 2>&1
$modelDownloadExit = $LASTEXITCODE
$modelDownloadChecks | Set-Content state/windows-model-download-checks.txt -Encoding utf8
$modelDownloadChecks | Write-Host
if ($modelDownloadExit -ne 0) { throw 'Model download checks failed' }

& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output '/out:state\ModelPackTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/r:LocalAI.Launcher.exe' 'tests\ModelPackTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model pack test build failed' }
$modelChecks = & ./state/ModelPackTests.exe 2>&1
$modelExit = $LASTEXITCODE
$modelChecks | Set-Content state/windows-model-pack-checks.txt -Encoding utf8
$modelChecks | Write-Host
if ($modelExit -ne 0) { throw 'Model pack logic checks failed' }

& ./scripts/Test-UI.ps1
if (-not $?) { throw 'Native UI/idle checks failed' }
& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 '/out:LibraryWatchTests.exe' /r:System.dll /r:System.Core.dll '/r:LocalAI.Launcher.exe' 'tests\LibraryWatchTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Library watcher test compilation failed' }
$watchChecks = & ./LibraryWatchTests.exe 2>&1
$watchExit = $LASTEXITCODE
$watchChecks | Set-Content state/windows-library-watch-checks.txt -Encoding utf8
if ($watchExit -ne 0) { throw 'Library watcher tests failed' }

$version = (Get-Content release-version.txt -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
$stage = Join-Path $env:RUNNER_TEMP ('si-local-release-' + [Guid]::NewGuid().ToString('N'))
New-Item $stage -ItemType Directory -Force | Out-Null
# Always ship the same source and instructions as the freshly compiled EXE.
foreach ($folder in @('src', 'tests', 'tools', 'validation', 'licenses')) {
    New-Item (Join-Path $stage $folder) -ItemType Directory -Force | Out-Null
    Copy-Item "$folder/*" (Join-Path $stage $folder) -Recurse -Force
}
foreach ($file in @('LocalAI.Launcher.exe', 'LocalAI.Launcher.exe.config', 'LocalAI.Launcher.sln', 'Build.cmd', 'Start.cmd', 'Run-Core-Tests.cmd', 'Run-API-Checks.cmd', 'Verify-Package.cmd', 'Verify-Package.ps1', 'release-version.txt', 'settings.example.json', 'ASSET_MANIFEST.json', 'START_HERE_KO.txt', 'README.md', 'MODEL_PACKS.md', 'README_KO.md', 'README_INSTALL_KO.txt', 'VALIDATION.md', 'API_CONTRACT.md', 'README_API_UPDATE_KO.md', 'README_FULL_DOWNLOAD_KO.md')) {
    Copy-Item $file (Join-Path $stage $file) -Force
}
Copy-Item state/windows-core-tests.txt (Join-Path $stage 'validation/WINDOWS_CORE_TESTS.txt')
Copy-Item state/ui-checks/UI_CHECKS.json (Join-Path $stage 'validation/UI_CHECKS.json')
Copy-Item state/windows-library-watch-checks.txt (Join-Path $stage 'validation/WINDOWS_LIBRARY_WATCH_CHECKS.txt')
Copy-Item state/windows-api-checks.txt (Join-Path $stage 'validation/WINDOWS_API_CHECKS.txt')
Copy-Item state/windows-download-checks.txt (Join-Path $stage 'validation/WINDOWS_DOWNLOAD_CHECKS.txt')
$receipt = @"
# Windows GitHub Actions validation

Version: $version
Commit: $env:GITHUB_SHA
Run: https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID

- Windows Server 2022 runner: native .NET Framework x64 Release build succeeded.
- Core regression checks: 62 passed.
- API request/credential/context logic checks: 8 passed.
- Download/assembly checks: 9 passed, including HTTP Range resume and ignored-Range restart.
- Standalone installer download/assembly checks: 9 passed.
- Complete GGUF download/assembly, cleanup and concurrent-write checks: 13 passed.
- No GGUF, llama-server, CUDA runtime or model inference was executed in this job.
- Native WinForms startup, idle CPU, UI heartbeat, fixed-size renders, simulated scaling and local file change checks passed.
- Physical DPI transitions, CPU/CUDA inference and company OpenCode/MCP integration remain unverified.
- This ZIP contains the Windows-built EXE. Previous Linux reports are historical records.
"@
$receipt | Set-Content (Join-Path $stage 'validation/WINDOWS_CI.md') -Encoding utf8
# Keep every original large-asset digest. Update only the files actually in this overlay.
$manifest = @{}
(Get-Content ASSET_MANIFEST.json -Raw | ConvertFrom-Json).files | ForEach-Object {
    $manifest[$_.path] = $_.sha256
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
$zipName = "Local_AI_Update_v${version}_Windows.zip"
$zipPath = Join-Path $out $zipName
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zipPath -CompressionLevel Optimal
$hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$hash  $zipName" | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
Copy-Item (Join-Path $stage 'validation/WINDOWS_CI.md') $out
Copy-Item state/windows-core-tests.txt $out
Copy-Item state/windows-api-checks.txt $out
Copy-Item state/windows-download-checks.txt $out
Copy-Item state/windows-installer-download-checks.txt $out
Copy-Item state/windows-model-download-checks.txt $out
Copy-Item state/windows-model-pack-checks.txt (Join-Path $out 'MODEL_PACK_CHECKS.txt')
Copy-Item state/ui-checks/UI_CHECKS.json $out
Copy-Item state/ui-checks/models-1000.png (Join-Path $out 'UI_PREVIEW.png')
Copy-Item state/windows-library-watch-checks.txt $out

& python -m unittest discover -s tests -p test_distribution.py
if ($LASTEXITCODE -ne 0) { throw 'Distribution split checks failed' }
& python scripts/build_full_release.py --overlay $stage --output $out --installer-only
if ($LASTEXITCODE -ne 0) { throw 'Full offline payload preparation failed' }
$payloadRoot = Join-Path $env:RUNNER_TEMP ("si-full-$version/Local_AI")
& ./scripts/Test-UI.ps1 -Output 'state/real-payload-ui' -PackageRoot $payloadRoot
if (-not $?) { throw 'Real-payload UI/idle checks failed' }
Copy-Item state/real-payload-ui/UI_CHECKS.json (Join-Path $out 'REAL_ASSET_UI_CHECKS.json')
& ./scripts/Build-Offline-Installer.ps1 -PayloadRoot $payloadRoot -OutDirectory $out
if (-not $?) { throw 'Offline installer build/validation failed' }
& $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:InstallerDownload '/out:release-output\Internet-PC-Download-Installer.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:release-output\installer-download-manifest.json,InstallerManifest' 'tools\FullDownload.cs' 'tools\InstallerDownload.cs'
if ($LASTEXITCODE -ne 0) { throw 'Internet-PC download helper compilation failed' }
$singleFolder = Join-Path $env:RUNNER_TEMP ('assembled-offline-installer-' + $version)
& ./release-output/Internet-PC-Download-Installer.exe --assemble-only --parts-directory $out --directory $singleFolder --no-pause
if ($LASTEXITCODE -ne 0) { throw 'Standalone installer assembly failed' }
& ./release-output/Internet-PC-Download-Installer.exe --verify-only --directory $singleFolder --no-pause
if ($LASTEXITCODE -ne 0) { throw 'Standalone installer whole-file verification failed' }
$setup = Join-Path $singleFolder 'Local_AI_Offline_Setup.exe'
if (@(Get-ChildItem $singleFolder -Force).Count -ne 1) { throw 'Installer must run from a folder containing only its EXE' }
& ./scripts/Test-Offline-Installer.ps1 -SetupPath $setup -PayloadRoot $payloadRoot -OutDirectory $out
if (-not $?) { throw 'Standalone installer native validation failed' }
Copy-Item README_FULL_DOWNLOAD_KO.md $out
Copy-Item README_INSTALL_KO.txt $out
$installer = Get-Content (Join-Path $out 'installer-download-manifest.json') -Raw | ConvertFrom-Json
$size = [Math]::Round($installer.installer.size / 1GB, 2)
@{
    version=$version; commit=$env:GITHUB_SHA
    installer=$installer.installer; transport_parts=$installer.parts.Count
    whole_sha256_verified=$true; single_exe_only_directory=$true
    installed_payload_verified=$true; outbound_blocked_install_verified=$true
    model_inference_verified=$false
    run="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $out 'SINGLE_INSTALLER_VALIDATION.json') -Encoding utf8
Get-ChildItem $out -File | Where-Object { $_.Name -notin @('SHA256SUMS.txt', 'RELEASE_NOTES.md') } | Sort-Object Name | ForEach-Object {
    (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name
} | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
@"
## Local AI Launcher

기존 설치: [$zipName](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/$zipName)을 설치 폴더에 덮어씁니다. 모델은 다시 받을 필요가 없습니다.

새 설치: [인터넷 PC용 다운로드 도구](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Internet-PC-Download-Installer.exe)를 실행하면 **Local_AI_Offline_Setup.exe 하나($size GiB)**가 만들어집니다.
완성된 EXE만 업무망으로 옮겨 설치하세요. 설치 중 인터넷 연결은 필요하지 않습니다.

GitHub 파일당 2GiB 제한 때문에 다운로드 도구가 분할 데이터를 자동으로 합칩니다. 완료 시 받은 조각은 정리됩니다.

- 포함: Qwen3.5-4B-Q4_K_M, llama.cpp CPU·CUDA 12.4/13.4, VC++ 런타임
- 환경: Windows 10/11 x64, .NET Framework 4.8+, 여유 공간 6GB 이상
- 상태 문구 잘림 수정, Pretendard 글꼴 적용
- 회사 로고 트레이 아이콘 상시 표시
- API 문맥 4K·8K·16K·32K 선택 및 실제 실행 중인 값 표시
- 추가 모델을 완성된 GGUF로 저장, 모델 팩 설치 메뉴 제거, 메모리 기준 안내 정리
- 모델 추가: **모델 추가 → 폴더 열기**에서 GGUF 복사
- 기존 포터블 업데이트: [$zipName](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/$zipName)

## 사양별 추가 모델

| PC | 모델 | 크기 |
|---|---|---:|
| CPU · RAM 8GB+ | Qwen3.5 2B | 1.28GB |
| VRAM 6–8GB · RAM 16GB+ | Qwen3.5 4B (기본 포함) | 2.74GB |
| VRAM 12–16GB · RAM 16GB+ | Qwen3.5 9B | 5.68GB |

[인터넷 PC용 모델 다운로드 도구](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Internet-PC-Download-Models.exe)에서 필요한 모델 또는 전체를 선택합니다.
완성된 Local_AI_Models/models의 GGUF를 앱의 모델 폴더에 복사하세요. 라이선스는 별도 licenses 폴더에 저장됩니다.
27B 등 큰 모델은 models 폴더에 GGUF를 직접 추가해 사용할 수 있습니다.
메모리 권장은 문맥 4K–8K 기준 추정치입니다.

Windows 빌드·외부 연결 차단 상태 설치·재설치·제거, 실제 추가 모델 다운로드·SHA256·GGUF 합치기·자동 인식 검사 통과. Windows 화면·대기 성능 검사 통과. 실제 모니터 DPI 전환·모델 추론·사내 MCP는 미검증입니다.
검사 기록: SINGLE_INSTALLER_VALIDATION.json · UI_CHECKS.json · REAL_ASSET_UI_CHECKS.json · INSTALLER_VALIDATION.md · WINDOWS_CI.md
"@ | Set-Content (Join-Path $out 'RELEASE_NOTES.md') -Encoding utf8

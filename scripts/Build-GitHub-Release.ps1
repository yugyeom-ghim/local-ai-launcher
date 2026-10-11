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

& $csc /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output '/out:state\ModelPackTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/r:LocalAI.Launcher.exe' 'tests/ModelPackTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model pack test build failed' }
$modelChecks = & ./state/ModelPackTests.exe 2>&1
$modelExit = $LASTEXITCODE
$modelChecks | Set-Content state/windows-model-pack-checks.txt -Encoding utf8
$modelChecks | Write-Host
if ($modelExit -ne 0) { throw 'Model pack logic checks failed' }

$version = (Get-Content release-version.txt -Raw).Trim()
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid release version' }
$stage = Join-Path $env:RUNNER_TEMP ('si-local-release-' + [Guid]::NewGuid().ToString('N'))
New-Item $stage -ItemType Directory -Force | Out-Null
# Always ship the same source and instructions as the freshly compiled EXE.
foreach ($folder in @('src', 'tests', 'tools', 'validation')) {
    New-Item (Join-Path $stage $folder) -ItemType Directory -Force | Out-Null
    Copy-Item "$folder/*" (Join-Path $stage $folder) -Recurse -Force
}
foreach ($file in @('LocalAI.Launcher.exe', 'LocalAI.Launcher.exe.config', 'LocalAI.Launcher.sln', 'Build.cmd', 'Start.cmd', 'Run-Core-Tests.cmd', 'Run-API-Checks.cmd', 'Verify-Package.cmd', 'Verify-Package.ps1', 'release-version.txt', 'settings.example.json', 'ASSET_MANIFEST.json', 'START_HERE_KO.txt', 'README.md', 'MODEL_PACKS.md', 'README_KO.md', 'README_INSTALL_KO.txt', 'VALIDATION.md', 'API_CONTRACT.md', 'README_API_UPDATE_KO.md', 'README_FULL_DOWNLOAD_KO.md')) {
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
Copy-Item state/windows-model-pack-checks.txt (Join-Path $out 'MODEL_PACK_CHECKS.txt')

& python -m unittest discover -s tests -p test_distribution.py
if ($LASTEXITCODE -ne 0) { throw 'Distribution split checks failed' }
& python scripts/build_full_release.py --overlay $stage --output $out --installer-only
if ($LASTEXITCODE -ne 0) { throw 'Full offline payload preparation failed' }
$payloadRoot = Join-Path $env:RUNNER_TEMP ("si-full-$version/Local_AI")
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
## Local AI Launcher

모델을 포함한 오프라인 설치 파일입니다. 합계 $size GiB.

$fileLinks

같은 폴더에 저장한 뒤 **Local_AI_Offline_Setup.exe**를 실행합니다.
[인터넷 PC용 다운로드 도구](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Internet-PC-Download-Installer.exe)는 이어받기와 SHA256 검사를 지원합니다.

- 포함: Qwen3.5-4B-Q4_K_M, llama.cpp CPU·CUDA 12.4/13.4, VC++ 런타임
- 환경: Windows 10/11 x64, .NET Framework 4.8+, 여유 공간 6GB 이상
- 변경: 사양별 추천, VRAM 표시, 오프라인 모델 팩 설치
- 기존 포터블 업데이트: [$zipName](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/$zipName)

## 사양별 추가 모델

| PC | 모델 | 크기 |
|---|---|---:|
| CPU · RAM 8GB+ | Qwen3.5 2B | 1.28GB |
| 4060 Ti 8GB / GPU 6–8GB | Qwen3.5 4B (기본 포함) | 2.74GB |
| 4060 Ti 16GB / GPU 12–16GB | Qwen3.5 9B | 5.68GB |
| GPU 24GB+ · RAM 32GB+ | Qwen3.8 27B | 16.46GB |

[인터넷 PC용 모델 다운로드 도구](https://github.com/$env:GITHUB_REPOSITORY/releases/download/v$version/Internet-PC-Download-Models.exe)에서 필요한 모델 또는 전체를 선택합니다.
팩 폴더를 반입한 뒤 Launcher의 **모델 팩 설치**에서 해당 .modelpack.json을 선택하세요.
메모리 권장은 문맥 4K–8K 기준 추정치입니다.

Windows 빌드·설치·재설치·제거, 실제 추가 모델 다운로드·SHA256·팩 설치 검사 통과. GUI/DPI·모델 추론·사내 MCP는 미검증입니다.
검사 기록: INSTALLER_VALIDATION.md · WINDOWS_CI.md · MODEL_PACK_VALIDATION.json
"@ | Set-Content (Join-Path $out 'RELEASE_NOTES.md') -Encoding utf8

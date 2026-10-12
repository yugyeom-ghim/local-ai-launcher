$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Set-Location (Split-Path -Parent $PSScriptRoot)
& cmd /d /c Build.cmd
if ($LASTEXITCODE -ne 0) { throw 'Preview build failed' }
& cmd /d /c 'Build.cmd /test'
if ($LASTEXITCODE -ne 0) { throw 'Core test build failed' }
& ./state/CoreTests.exe ./state/windows-core-tests.txt
if ($LASTEXITCODE -ne 0) { throw 'Core checks failed' }
& cmd /d /c 'Build.cmd /apitest'
if ($LASTEXITCODE -ne 0) { throw 'API test build failed' }
$api = & ./state/ApiChecks.exe ./state 2>&1
$apiExit = $LASTEXITCODE
$api | Set-Content state/windows-api-checks.txt -Encoding utf8
$api | Write-Host
if ($apiExit -ne 0) { throw 'API/context checks failed' }
& ./scripts/Test-UI.ps1
if (-not $?) { throw 'UI/font/tray/idle checks failed' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$web = (Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL/System.Web.Extensions') -Filter System.Web.Extensions.dll -Recurse | Select-Object -First 1).FullName
& $csc /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:ModelDownloadTests '/out:state\ModelDownloadTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" tools/FullDownload.cs tools/ModelDownload.cs tests/ModelDownloadTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Model downloader test build failed' }
$checks = & ./state/ModelDownloadTests.exe 2>&1
$checksExit = $LASTEXITCODE
$checks | Set-Content state/windows-model-download-checks.txt -Encoding utf8
$checks | Write-Host
if ($checksExit -ne 0) { throw 'Model download checks failed' }
& $csc /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 '/out:LibraryWatchTests.exe' /r:System.dll /r:System.Core.dll '/r:LocalAI.Launcher.exe' tests/LibraryWatchTests.cs
if ($LASTEXITCODE -ne 0) { throw 'Watcher test build failed' }
& ./LibraryWatchTests.exe
if ($LASTEXITCODE -ne 0) { throw 'Watcher checks failed' }
$out = Join-Path $PWD 'preview-output'
$stage = Join-Path $env:RUNNER_TEMP ('local-ai-preview-' + $env:GITHUB_SHA)
New-Item $out,$stage,(Join-Path $stage 'licenses') -ItemType Directory -Force | Out-Null
Copy-Item LocalAI.Launcher.exe,LocalAI.Launcher.exe.config $stage
Copy-Item licenses/Pretendard-LICENSE.txt (Join-Path $stage 'licenses')
@'
검증용 업데이트

앱을 종료하고 ZIP 내용을 기존 설치 폴더에 덮어쓴 뒤 LocalAI.Launcher.exe를 실행하세요.
확인할 항목: 준비 완료 문구, 글꼴, 트레이 아이콘, 모델 추가, API 문맥 크기.
문맥을 바꿨다면 서버 재시작 후 OpenCode 설정을 다시 내보내고 새 대화에서 확인하세요.
'@ | Set-Content (Join-Path $stage 'README_TEST_KO.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $out 'Local_AI_Preview_Windows.zip') -CompressionLevel Optimal
& $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:ModelDownload '/out:preview-output\Internet-PC-Download-Models.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:tools\model-downloads.json,ModelDownloads' tools/FullDownload.cs tools/ModelDownload.cs
if ($LASTEXITCODE -ne 0) { throw 'Model download helper compilation failed' }
& ./preview-output/Internet-PC-Download-Models.exe --list --no-pause
if ($LASTEXITCODE -ne 0) { throw 'Pinned model download manifest invalid' }
Copy-Item state/ui-checks/UI_CHECKS.json,state/windows-api-checks.txt,state/windows-model-download-checks.txt $out
foreach ($image in @('models-1000','models-800','models-scale150','page-1','model-guide')) { Copy-Item "state/ui-checks/$image.png" $out }
@{
    commit=$env:GITHUB_SHA; run="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    native_windows_build=$true; core_checks=62; api_checks=8; model_download_checks=13; watcher_checks=7
    embedded_font='Pretendard 1.3.9'; ui_checks='UI_CHECKS.json'
    ready_status_is_simulated=$true; live_model_inference=$false; company_opencode_mcp=$false; full_installer_built=$false
    model_transport_release='v1.6.1'; real_model_download_repeated=$false
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'PREVIEW_VALIDATION.json') -Encoding utf8
Get-ChildItem $out -File | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name } | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
@'
## 검증용 업데이트

Local_AI_Preview_Windows.zip을 기존 설치 폴더에 덮어써서 확인하세요.

- 상태 문구 잘림 수정, Pretendard 글꼴 적용
- 회사 로고 트레이 아이콘 상시 표시
- API 문맥 선택과 실제 실행 중인 문맥 표시
- GGUF 복사로 모델 추가, 모델팩 설치 메뉴 제거

Internet-PC-Download-Models.exe는 인터넷 PC에서 완성된 GGUF를 받는 도구입니다.
Windows 빌드·UI·문맥 설정·파일 감지 검사를 통과했습니다. 실제 모델 추론과 사내 OpenCode/MCP는 미검증입니다.
설치 파일은 확인 후 제작합니다.
'@ | Set-Content (Join-Path $out 'REVIEW_NOTES.md') -Encoding utf8

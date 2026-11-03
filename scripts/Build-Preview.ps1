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
& ./scripts/Test-Cors.ps1
if (-not $?) { throw 'CORS checks failed' }
& ./scripts/Test-UI.ps1
if (-not $?) { throw 'UI/font/tray/idle checks failed' }
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$web = (Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL/System.Web.Extensions') -Filter System.Web.Extensions.dll -Recurse | Select-Object -First 1).FullName
& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output /main:ModelDownloadTests '/out:state\ModelDownloadTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" 'tools\FullDownload.cs' 'tools\ModelDownload.cs' 'tests\ModelDownloadTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model downloader test build failed' }
$checks = & ./state/ModelDownloadTests.exe 2>&1
$checksExit = $LASTEXITCODE
$checks | Set-Content state/windows-model-download-checks.txt -Encoding utf8
$checks | Write-Host
if ($checksExit -ne 0) { throw 'Model download checks failed' }
& $csc /nologo /noconfig /target:exe /platform:x64 /langversion:5 /codepage:65001 '/out:LibraryWatchTests.exe' /r:System.dll /r:System.Core.dll '/r:LocalAI.Launcher.exe' 'tests\LibraryWatchTests.cs'
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
최소화 업데이트: 일반 최소화는 작업표시줄에 유지됩니다.
상단의 트레이로 보내기 버튼을 누르면 알림 영역으로 숨깁니다.
트레이 아이콘 더블클릭 또는 열기 메뉴로 창을 다시 표시합니다.
기존 256K·문맥 직접 입력 기능도 포함됩니다.
32K 상한을 제거했습니다. 직접 입력은 512~2,147,483,647 정수 범위이며 실제 사용 가능 크기를 보장하지 않습니다.
큰 문맥은 메모리와 처리 시간이 더 필요합니다. GGUF 기준 초과 시 YaRN을 자동 적용하지 않습니다.
문맥을 바꿨다면 서버 재시작 후 OpenCode 설정을 다시 내보내고 새 대화에서 확인하세요.
'@ | Set-Content (Join-Path $stage 'README_TEST_KO.txt') -Encoding utf8
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath (Join-Path $out 'Local_AI_Preview_Windows.zip') -CompressionLevel Optimal
& $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:ModelDownload '/out:preview-output\Internet-PC-Download-Models.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:tools\model-downloads.json,ModelDownloads' 'tools\FullDownload.cs' 'tools\ModelDownload.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model download helper compilation failed' }
& ./preview-output/Internet-PC-Download-Models.exe --list --no-pause
if ($LASTEXITCODE -ne 0) { throw 'Pinned model download manifest invalid' }
Copy-Item state/ui-checks/UI_CHECKS.json,state/windows-api-checks.txt,state/windows-model-download-checks.txt,state/windows-cors-checks.txt $out
foreach ($image in @('models-1000','models-800','models-scale150','page-1','model-guide')) { Copy-Item "state/ui-checks/$image.png" $out }
@{
    commit=$env:GITHUB_SHA; run="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"
    native_windows_build=$true; core_checks=69; cors_checks=30; api_checks=8; model_download_checks=13; watcher_checks=7
    embedded_font='Pretendard 1.3.9'; ui_checks='UI_CHECKS.json'
    ready_status_is_simulated=$true; live_model_inference=$false; company_opencode_mcp=$false; full_installer_built=$false
    model_transport_release='v1.6.2'; real_model_download_repeated=$false
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $out 'PREVIEW_VALIDATION.json') -Encoding utf8
Get-ChildItem $out -File | Where-Object { $_.Name -ne 'SHA256SUMS.txt' } | ForEach-Object { (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_.Name } | Set-Content (Join-Path $out 'SHA256SUMS.txt') -Encoding ascii
@'
## 검증용 업데이트

Local_AI_Preview_Windows.zip을 기존 설치 폴더에 덮어써서 확인하세요.

- 일반 최소화 시 작업표시줄에 창 유지
- 상단에 트레이로 보내기 버튼 추가
- 트레이 열기/더블클릭으로 복원, 최대화 상태 보존
- 종료 확인창의 아니요는 명시적으로 트레이로 보내기
- 256K·문맥 직접 입력·GGUF 기준값 표시 유지

앱을 완전히 종료하고 덮어쓴 뒤 서버를 다시 시작하세요. 모델 재다운로드는 필요 없습니다.
문맥 변경 후 OpenCode 설정을 다시 내보내세요. 기준 초과 입력만으로 YaRN이 자동 적용되지는 않습니다.
직접 입력 범위는 512~2,147,483,647이며 실제 사용 가능 크기는 모델과 PC에 따라 다릅니다.

Internet-PC-Download-Models.exe는 인터넷 PC에서 완성된 GGUF를 받는 도구입니다.
Windows 빌드·UI·문맥 설정·파일 감지 검사를 통과했습니다. 실제 모델 추론과 사내 Confluence 브라우저·OpenCode/MCP는 미검증입니다.
설치 파일은 확인 후 제작합니다.
'@ | Set-Content (Join-Path $out 'REVIEW_NOTES.md') -Encoding utf8

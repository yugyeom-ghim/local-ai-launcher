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
문맥 업데이트: 64K·128K·256K 선택, 토큰 수 직접 입력, GGUF 기준값 표시 및 적용.
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

- 64K·128K·256K 문맥 프리셋과 임의 토큰 수 직접 입력
- GGUF의 모델별 기준 문맥 표시 및 기준값 적용 버튼
- 기존 저장값과 기본 4K 유지, 모델 변경 시 사용자 입력 유지
- 자동 메모리 맞춤이 요청 문맥을 줄이지 않도록 최소 문맥도 함께 전달
- 설정 저장·서버 인자·OpenCode 내보내기·UI 동기화 검사

앱을 완전히 종료하고 덮어쓴 뒤 서버를 다시 시작하세요. 모델 재다운로드는 필요 없습니다.
문맥 변경 후 OpenCode 설정을 다시 내보내세요. 기준 초과 입력만으로 YaRN이 자동 적용되지는 않습니다.
직접 입력 범위는 512~2,147,483,647이며 실제 사용 가능 크기는 모델과 PC에 따라 다릅니다.

Internet-PC-Download-Models.exe는 인터넷 PC에서 완성된 GGUF를 받는 도구입니다.
Windows 빌드·UI·문맥 설정·파일 감지 검사를 통과했습니다. 실제 모델 추론과 사내 Confluence 브라우저·OpenCode/MCP는 미검증입니다.
설치 파일은 확인 후 제작합니다.
'@ | Set-Content (Join-Path $out 'REVIEW_NOTES.md') -Encoding utf8

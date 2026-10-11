$ErrorActionPreference = 'Stop'
foreach ($tag in @('v1.3.0', 'v1.3.1', 'v1.4.0')) {
    $raw = gh release view $tag --repo $env:GITHUB_REPOSITORY --json assets
    if ($LASTEXITCODE -ne 0) { throw "Cannot read release: $tag" }
    $release = $raw | ConvertFrom-Json
    $files = @($release.assets | Where-Object { $_.name -match '\.(exe|bin|zip|part\d+)$' })
    $links = ($files | ForEach-Object {
        '- [' + $_.name + '](https://github.com/' + $env:GITHUB_REPOSITORY + '/releases/download/' + $tag + '/' + $_.name + ')'
    }) -join "`n"
    $usage = switch ($tag) {
        'v1.3.0' { '기존 포터블 설치 폴더에 Update ZIP을 덮어쓰는 배포본입니다.' }
        'v1.3.1' { 'Download-SI-Local-AI.exe는 인터넷 PC에서 전체 ZIP을 받는 도구입니다.' }
        'v1.4.0' { 'Setup EXE와 BIN을 같은 폴더에 두고 Setup을 실행합니다. 설치는 오프라인으로 진행됩니다.' }
    }
    $body = @"
Local AI Launcher 이전 배포본입니다. [최신 설치 파일](https://github.com/$env:GITHUB_REPOSITORY/releases/latest)

$usage

$links

검증 기록: WINDOWS_CI.md
"@
    $path = Join-Path $env:RUNNER_TEMP "release-notes-$tag.md"
    $body | Set-Content $path -Encoding utf8
    gh release edit $tag --repo $env:GITHUB_REPOSITORY --title "Local AI Launcher $tag" --notes-file $path
    if ($LASTEXITCODE -ne 0) { throw "Cannot update release notes: $tag" }
}

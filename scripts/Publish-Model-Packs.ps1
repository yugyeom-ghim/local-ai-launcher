param([Parameter(Mandatory=$true)][string]$Tag)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
$out = Join-Path $repoRoot 'release-output'
$volume = Get-PSDrive -PSProvider FileSystem | Where-Object { $_.Free -gt 36GB } | Sort-Object Free -Descending | Select-Object -First 1
if (-not $volume) { throw 'A build volume with 36 GiB free is required for the largest pack import check' }
$buildRoot = Join-Path $volume.Root ('local-ai-model-packs-' + [Guid]::NewGuid().ToString('N'))
$packRoot = Join-Path $buildRoot 'packs'
$catalog = Get-Content src/Resources/ModelCatalog.json -Raw | ConvertFrom-Json
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$csc = Join-Path $framework 'csc.exe'
$web = (Get-ChildItem (Join-Path $env:WINDIR 'Microsoft.NET/assembly/GAC_MSIL/System.Web.Extensions') -Filter System.Web.Extensions.dll -Recurse | Select-Object -First 1).FullName
& $csc /nologo /target:exe /platform:x64 /langversion:5 /codepage:65001 /utf8output '/out:state\ModelPackTests.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/r:LocalAI.Launcher.exe' 'tests/ModelPackTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Model pack test build failed' }
$checks = & ./state/ModelPackTests.exe 2>&1
$exit = $LASTEXITCODE
$checks | Set-Content (Join-Path $out 'MODEL_PACK_CHECKS.txt') -Encoding utf8
if ($exit -ne 0) { throw 'Model pack checks failed' }
$downloads = @()
$records = @()
$hashes = @()
foreach ($model in @($catalog.models | Where-Object { -not $_.bundled })) {
    $folder = Join-Path $packRoot $model.id
    & python scripts/build_model_pack.py --model $model.id --output $folder
    if ($LASTEXITCODE -ne 0) { throw "Model download/hash verification failed: $($model.id)" }
    $manifest = Join-Path $folder ($model.id + '.modelpack.json')
    $installed = Join-Path $buildRoot ('모델 검증/' + $model.id)
    $result = & ./state/ModelPackTests.exe $manifest $installed 2>&1
    $exit = $LASTEXITCODE
    $result | Add-Content (Join-Path $out 'MODEL_PACK_CHECKS.txt') -Encoding utf8
    $result | Write-Host
    if ($exit -ne 0) { throw "Windows model import failed: $($model.id)" }
    Remove-Item $installed -Recurse -Force
    $baseUrl = "https://github.com/$env:GITHUB_REPOSITORY/releases/download/$Tag/"
    $files = @(Get-ChildItem $folder -File | Sort-Object Name | ForEach-Object {
        @{ name=$_.Name; size=$_.Length; sha256=(Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); url=$baseUrl+$_.Name }
    })
    $downloads += @{ id=$model.id; name=$model.name; tier=$model.tier; files=$files }
    @{ models=$downloads } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $out 'model-download-manifest.json') -Encoding utf8
    & $csc /nologo /noconfig /target:exe /platform:x64 /optimize+ /langversion:5 /codepage:65001 /utf8output /main:ModelDownload '/out:release-output\Internet-PC-Download-Models.exe' /r:System.dll /r:System.Core.dll "/r:$web" '/resource:release-output\model-download-manifest.json,ModelDownloads' 'tools\FullDownload.cs' 'tools\ModelDownload.cs'
    if ($LASTEXITCODE -ne 0) { throw 'Model download helper compilation failed' }
    & ./release-output/Internet-PC-Download-Models.exe --model $model.id --directory $packRoot --verify-only --no-pause
    if ($LASTEXITCODE -ne 0) { throw 'Model helper manifest verification failed' }
    Remove-Item (Join-Path $folder 'download.lock')
    $assets = @(Get-ChildItem $folder -File | ForEach-Object { $_.FullName })
    & gh release upload $Tag @assets --repo $env:GITHUB_REPOSITORY --clobber
    if ($LASTEXITCODE -ne 0) { throw 'Model pack upload failed; release remains draft' }
    $records += @{ id=$model.id; file=$model.file; size=$model.size; sha256=$model.sha256; revision=$model.revision; download_verified=$true; windows_import_verified=$true; gguf_header_and_tensor_bounds_verified=$true; discovery_and_selected_path_verified=$true; inference_verified=$false }
    $hashes += @($files | ForEach-Object { $_.sha256 + '  ' + $_.name })
    # Each pack is uploaded and validated before reclaiming disk for the next model.
    Remove-Item $folder -Recurse -Force
}
@{ commit=$env:GITHUB_SHA; run="https://github.com/$env:GITHUB_REPOSITORY/actions/runs/$env:GITHUB_RUN_ID"; logic_checks=12; models=$records; gpu_inference_verified=$false } | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $out 'MODEL_PACK_VALIDATION.json') -Encoding utf8
Copy-Item src/Resources/ModelCatalog.json (Join-Path $out 'MODEL_CATALOG.json')
Copy-Item MODEL_PACKS.md $out
$finalNames = @('Internet-PC-Download-Models.exe','model-download-manifest.json','MODEL_CATALOG.json','MODEL_PACKS.md','MODEL_PACK_VALIDATION.json','MODEL_PACK_CHECKS.txt')
$hashes += @($finalNames | ForEach-Object { (Get-FileHash (Join-Path $out $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_ })
$hashes | Set-Content (Join-Path $out 'MODEL_PACK_SHA256SUMS.txt') -Encoding ascii
$finalFiles = @($finalNames + 'MODEL_PACK_SHA256SUMS.txt' | ForEach-Object { Join-Path $out $_ })
& gh release upload $Tag @finalFiles --repo $env:GITHUB_REPOSITORY --clobber
if ($LASTEXITCODE -ne 0) { throw 'Model pack metadata upload failed' }
Remove-Item $buildRoot -Recurse -Force

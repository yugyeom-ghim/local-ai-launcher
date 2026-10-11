$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
$count = 0
foreach ($line in [IO.File]::ReadLines((Join-Path $root 'SHA256SUMS.txt'))) {
    if ($line -notmatch '^([0-9a-f]{64})  (.+)$') { throw 'Invalid checksum manifest line.' }
    $expected = $Matches[1]
    $relative = $Matches[2]
    $path = [IO.Path]::GetFullPath((Join-Path $root $relative))
    if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe checksum path.' }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing: $relative" }
    $actual = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($actual -ne $expected) { throw "SHA256 mismatch: $relative" }
    $count++
    Write-Host "OK $relative"
}
Write-Host "Verified $count packaged files. No server/model was executed."

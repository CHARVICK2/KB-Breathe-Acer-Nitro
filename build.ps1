# Builds KbBreathe.exe (single tiny file), then zips it and splits the zip into 3 parts.
# Usage:  build.bat   (or: powershell -ExecutionPolicy Bypass -File build.ps1)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$out  = Join-Path $PSScriptRoot 'publish'
$dist = Join-Path $PSScriptRoot 'dist'
Remove-Item $out, $dist, (Join-Path $PSScriptRoot 'bin'), (Join-Path $PSScriptRoot 'obj') -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $dist | Out-Null

dotnet build -c Release -o $out
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }

$exe = Join-Path $out 'KbBreathe.exe'
Copy-Item $exe (Join-Path $dist 'KbBreathe.exe')      # the plain single exe, ready to use

$zip = Join-Path $dist 'KbBreathe.zip'
Compress-Archive -Path $exe -DestinationPath $zip -CompressionLevel Optimal

# ---- split into 3 parts ----
$bytes = [IO.File]::ReadAllBytes($zip)
$size  = [int][math]::Ceiling($bytes.Length / 3.0)
for ($i = 0; $i -lt 3; $i++) {
    $start = $i * $size
    $len   = [int][math]::Min($size, $bytes.Length - $start)
    $part  = Join-Path $dist ('KbBreathe.part{0:00}' -f $i)
    $fs = [IO.File]::Create($part)
    try { $fs.Write($bytes, $start, $len) } finally { $fs.Close() }
}

# ---- verify that joining gives back the identical zip ----
$ms = New-Object IO.MemoryStream
0..2 | ForEach-Object {
    $p = [IO.File]::ReadAllBytes((Join-Path $dist ('KbBreathe.part{0:00}' -f $_)))
    $ms.Write($p, 0, $p.Length)
}
$sha = [Security.Cryptography.SHA256]::Create()
if ([BitConverter]::ToString($sha.ComputeHash($ms.ToArray())) -ne [BitConverter]::ToString($sha.ComputeHash($bytes))) { throw 'Split verification FAILED' }
Remove-Item $zip

Write-Host ''
Write-Host 'Done. Files in:' $dist -ForegroundColor Green
Get-ChildItem $dist | ForEach-Object { '{0}  {1:N1} KB' -f $_.Name, ($_.Length / 1KB) }
Write-Host ''
Write-Host 'To join the 3 parts, run in that folder:'
Write-Host '  cmd /c copy /b KbBreathe.part00 + KbBreathe.part01 + KbBreathe.part02 KbBreathe.zip'

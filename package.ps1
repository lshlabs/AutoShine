param([string]$Dotnet = 'dotnet', [string]$Iscc = 'ISCC.exe')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $compiler = (Get-Command $Iscc -ErrorAction Stop).Source
    & (Join-Path $PSScriptRoot 'build.ps1') -Dotnet $Dotnet
    if (!$?) { throw 'Release build failed' }
    Copy-Item -LiteralPath RELEASE_NOTES.md -Destination artifacts/package/RELEASE_NOTES.md
    & $compiler installer/AutoShine.iss
    if ($LASTEXITCODE) { throw 'Installer compilation failed' }
    $releaseDir = Join-Path $PSScriptRoot 'releases/v1.0.0'
    New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
    $setup = Join-Path $releaseDir 'AutoShine-1.0.0-Setup.exe'
    Copy-Item -LiteralPath artifacts/installer/AutoShine-1.0.0-Setup.exe -Destination $setup -Force
    Copy-Item -LiteralPath RELEASE_NOTES.md -Destination (Join-Path $releaseDir 'RELEASE_NOTES.md') -Force
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath (Join-Path $releaseDir 'SHA256SUMS.txt') -Value "$hash  AutoShine-1.0.0-Setup.exe" -Encoding ascii
    Write-Host "Release ready: $releaseDir"
} finally { Pop-Location }

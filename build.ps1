param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & npm.cmd ci
    if ($LASTEXITCODE) { throw 'UI dependency installation failed' }
    & npm.cmd run build:ui
    if ($LASTEXITCODE) { throw 'UI build failed' }
    & node tests/ui-api.test.cjs
    if ($LASTEXITCODE) { throw 'UI API checks failed' }
    & npm.cmd exec -- tsc -p ui/tsconfig.model-tests.json
    if ($LASTEXITCODE) { throw 'UI model test compilation failed' }
    & node tests/ui-model.test.cjs
    if ($LASTEXITCODE) { throw 'UI model checks failed' }
    & npm.cmd exec -- tsc ui/src/i18n/translate.ts ui/src/i18n/clarified.ts ui/src/i18n/completed.ts --outDir artifacts/i18n-check --module commonjs --target es2022 --skipLibCheck
    if ($LASTEXITCODE) { throw 'UI i18n test compilation failed' }
    & node tests/ui-i18n.test.cjs
    if ($LASTEXITCODE) { throw 'UI i18n checks failed' }
    & $Dotnet build AutoShine.slnx -c Release
    if ($LASTEXITCODE) { throw 'Solution build failed' }
    & $Dotnet run --project tests/AutoShine.Tests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Core tests failed' }
    & $Dotnet run --project tests/AutoShine.IntegrationTests -c Release --no-build
    if ($LASTEXITCODE) { throw 'Windows integration tests failed' }
    # Clear only this generated static-output directory so obsolete GUI assets cannot ship.
    $staticOutput = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts/package/wwwroot'))
    $packageRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts/package'))
    if ([IO.Path]::GetDirectoryName($packageRoot) -ne [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'artifacts')) -or [IO.Path]::GetFileName($packageRoot) -ne 'package') { throw 'Invalid package path' }
    if (Test-Path -LiteralPath $packageRoot) { Remove-Item -LiteralPath $packageRoot -Recurse -Force }
    if ([IO.Path]::GetDirectoryName($staticOutput) -ne $packageRoot -or [IO.Path]::GetFileName($staticOutput) -ne 'wwwroot') { throw 'Invalid static output path' }
    if (Test-Path -LiteralPath $staticOutput) { Remove-Item -LiteralPath $staticOutput -Recurse -Force }
    & $Dotnet publish src/AutoShine.Web -c Release -r win-x64 --self-contained true -o artifacts/package
    if ($LASTEXITCODE) { throw 'Web publish failed' }
    foreach ($legacyFile in @('renew.js', 'renew.css')) {
        $legacyPath = Join-Path $PSScriptRoot "artifacts/package/wwwroot/$legacyFile"
        if (Test-Path -LiteralPath $legacyPath) { Remove-Item -LiteralPath $legacyPath }
    }
    & $Dotnet publish src/AutoShine.Agent -c Release -r win-x64 --self-contained true -o artifacts/package
    if ($LASTEXITCODE) { throw 'Agent publish failed' }
    Get-ChildItem -LiteralPath $packageRoot -Recurse -File -Filter '*.pdb' | Remove-Item -Force
    Copy-Item -LiteralPath README.md -Destination artifacts/package/README.md
    $dotnetRoot = Split-Path (Get-Command $Dotnet -ErrorAction Stop).Source
    New-Item -ItemType Directory -Force -Path artifacts/package/licenses | Out-Null
    foreach ($notice in @('LICENSE.txt','ThirdPartyNotices.txt')) {
        $noticePath = Join-Path $dotnetRoot $notice
        if (!(Test-Path -LiteralPath $noticePath)) { throw "Missing .NET license notice: $notice" }
        Copy-Item -LiteralPath $noticePath -Destination (Join-Path 'artifacts/package/licenses' "dotnet-$notice")
    }
    Write-Host 'Ready: artifacts/package/AutoShine.exe'
} finally { Pop-Location }

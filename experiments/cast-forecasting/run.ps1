param([string]$PackageCache = '')
$ErrorActionPreference = 'Stop'
$experimentRoot = $PSScriptRoot
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $experimentRoot '../..'))
if ($PackageCache) { $env:NUGET_PACKAGES = $PackageCache }
function Source-Fingerprint {
    $files = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src/HollowWake.Core') -Recurse -Filter '*.cs' |
        Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' })
    $files += @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'content/core') -Recurse -Filter '*.yaml')
    return (($files | Sort-Object FullName | ForEach-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }) -join '')
}
$before = Source-Fingerprint
dotnet run --project (Join-Path $experimentRoot 'ForecastProbe.csproj') -- $repositoryRoot
if ($LASTEXITCODE -ne 0) { throw 'Core scenario probe failed' }
if ((Source-Fingerprint) -ne $before) { throw 'Shared core/content changed during the probe. Rerun before relying on its evidence.' }
node --test (Join-Path $experimentRoot 'contract-model.test.mjs')
if ($LASTEXITCODE -ne 0) { throw 'Proposed contract checks failed' }

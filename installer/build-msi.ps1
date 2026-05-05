# Publish WinRecorder for win-x64, then build the WiX MSI into installer/bin/<Configuration>/.
# Run from repo root: powershell -ExecutionPolicy Bypass -File installer\build-msi.ps1

param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Release',

    [switch] $SelfContained
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot 'artifacts\publish'
$csproj = Join-Path $repoRoot 'src\WinRecorder\WinRecorder.csproj'
$wixproj = Join-Path $repoRoot 'installer\WinRecorder.Installer.wixproj'

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
    'publish', $csproj,
    '-c', $Configuration,
    '-r', 'win-x64',
    '-o', $publishDir
)
if ($SelfContained) {
    $publishArgs += '--self-contained', 'true'
} else {
    $publishArgs += '--self-contained', 'false'
}

Write-Host "dotnet $($publishArgs -join ' ')"
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$publishFull = (Resolve-Path -LiteralPath $publishDir).Path
if (-not $publishFull.EndsWith('\')) { $publishFull += '\' }
Write-Host "Building MSI (harvest from $publishFull) ..."

dotnet build $wixproj -c $Configuration /p:AppPublishDir="$publishFull"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$msi = Get-ChildItem -Path (Join-Path $repoRoot 'installer\bin') -Recurse -Filter 'WinRecorder.Installer.msi' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $msi) {
    Write-Error "MSI not found under $msiDir"
}
Write-Host "MSI: $($msi.FullName)"

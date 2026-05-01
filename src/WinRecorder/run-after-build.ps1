#!/usr/bin/env pwsh
# Build WinRecorder (unless -NoBuild / -ExePath only) then start the executable.
# MSBuild passes -ExePath for LaunchAfterBuild; do not rename without updating the csproj.

param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [switch] $NoBuild,

    # When set (e.g. by MSBuild), skip build and launch this exe.
    [string] $ExePath = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectDir = Split-Path -Parent $PSCommandPath

if (-not $ExePath) {
    if (-not $NoBuild) {
        dotnet build "$projectDir\WinRecorder.csproj" -c $Configuration
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    $ExePath = Join-Path $projectDir "bin\$Configuration\net8.0-windows\WinRecorder.exe"
}

if (-not (Test-Path -LiteralPath $ExePath)) {
    Write-Error "Executable not found: $ExePath"
}

$workingDir = Split-Path -Parent $ExePath
Start-Process -FilePath $ExePath -WorkingDirectory $workingDir

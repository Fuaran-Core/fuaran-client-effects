#Requires -Version 7.0
<#
.SYNOPSIS
    Check the Fuaran.ClientEffects repository: tools, formatting, build, tests and the Fable compile.

.DESCRIPTION
    Drop into this directory and run one command: `pwsh ./run.ps1`. Needs PowerShell 7 and the .NET SDK that
    global.json pins. Stages, in order; the first failure stops the run and exits non-zero, naming the stage:

      1. tools    dotnet tool restore (Fantomas, Fable).
      2. format   fantomas --check over src/ and tests/.
      3. build    dotnet build Fuaran.ClientEffects.slnx.
      4. tests    the Expecto suite: the program specification's client-effect corpus against the codec, and the
                  codec's laws. Set FUARAN_PROGRAM_SPEC_CORPUS to a checkout's wire-fixtures directory to add the
                  check that the embedded copy of the corpus is the live manifest's family.
      5. fable    the library compiled with the repository's `fable` tool, from the sources its package ships, so a
                  Fable consumer compiles what this stage compiled.

    Every stage seeds $global:LASTEXITCODE before it runs a native command, so a stage that is skipped, or that runs
    no native command, cannot inherit another stage's verdict. A skipped stage reports SKIPPED, never PASS.

.PARAMETER SkipFormat
    Skip stage 2 while iterating.

.PARAMETER SkipBuild
    Skip stage 3. Stage 4 then builds what it needs itself.

.PARAMETER SkipTests
    Skip stage 4.

.PARAMETER SkipFable
    Skip stage 5.
#>
[CmdletBinding()]
param(
    [switch] $SkipFormat,
    [switch] $SkipBuild,
    [switch] $SkipTests,
    [switch] $SkipFable
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)

function Write-Stage([string] $Name) { Write-Host "`n=== $Name ===" -ForegroundColor Cyan }
function Write-Skip([string] $Name, [string] $Why) { Write-Host "`n=== ${Name}: SKIPPED ($Why) ===" -ForegroundColor Yellow }

function Stop-OnFailure([string] $Name) {
    if ($global:LASTEXITCODE -ne 0) {
        Write-Host "FAIL - $Name (exit $global:LASTEXITCODE)" -ForegroundColor Red
        exit $global:LASTEXITCODE
    }
    Write-Host "PASS - $Name" -ForegroundColor Green
}

# 1. tools
Write-Stage "tools"
$global:LASTEXITCODE = 0
dotnet tool restore
Stop-OnFailure "tools"

# 2. format
if ($SkipFormat) {
    Write-Skip "format" "-SkipFormat"
}
else {
    Write-Stage "format"
    $global:LASTEXITCODE = 0
    dotnet fantomas --check src tests
    Stop-OnFailure "format"
}

# 3. build
if ($SkipBuild) {
    Write-Skip "build" "-SkipBuild"
}
else {
    Write-Stage "build"
    $global:LASTEXITCODE = 0
    dotnet build Fuaran.ClientEffects.slnx --nologo
    Stop-OnFailure "build"
}

# 4. tests
if ($SkipTests) {
    Write-Skip "tests" "-SkipTests"
}
else {
    Write-Stage "tests"
    $global:LASTEXITCODE = 0
    if ($SkipBuild) {
        dotnet run --project tests/Fuaran.ClientEffects.Tests -- --summary
    }
    else {
        dotnet run --project tests/Fuaran.ClientEffects.Tests --no-build -- --summary
    }
    Stop-OnFailure "tests (Fuaran.ClientEffects.Tests)"
}

# 5. fable
if ($SkipFable) {
    Write-Skip "fable" "-SkipFable"
}
else {
    Write-Stage "fable"
    $out = Join-Path "artifacts" "fable"
    $global:LASTEXITCODE = 0
    dotnet fable (Join-Path "src" "Fuaran.ClientEffects" "Fuaran.ClientEffects.fsproj") -o $out --noCache
    Stop-OnFailure "fable (compile src/Fuaran.ClientEffects)"
}

Write-Host "`nEvery stage that ran passed." -ForegroundColor Green

#Requires -Version 7.0
# Repository verify gate: a pass-through to run.ps1, so the gate and the one-command entry point cannot diverge.
# The checks live in run.ps1, which is what a contributor runs; this file forwards its arguments and carries its exit code.
$ErrorActionPreference = "Stop"
$global:LASTEXITCODE = 0
& (Join-Path $PSScriptRoot "run.ps1") @args
exit $LASTEXITCODE

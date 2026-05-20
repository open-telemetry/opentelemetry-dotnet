#! /usr/bin/env pwsh

#Requires -PSEdition Core
#Requires -Version 7

param(
    [Parameter(Mandatory = $true, Position = 0)][string[]] $Benchmarks,
    [Parameter(Mandatory = $false)][string] $Target,
    [Parameter(Mandatory = $false)][string] $Baseline = "main",
    [Parameter(Mandatory = $false)][string] $Job = "Default",
    [Parameter(Mandatory = $false)][string[]] $Runtimes = @("net10.0"),
    [Parameter(Mandatory = $false)][switch] $EnableMemoryDiagnoser,
    [Parameter(Mandatory = $false)][switch] $EnableEventPipeProfiler
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

$Configuration = "Release"
$Framework = "net10.0"

if ($Benchmarks.Count -eq 0) {
    throw "At least one benchmark filter must be specified."
}

if ($Runtimes.Count -eq 0) {
    throw "At least one .NET runtime must be specified."
}

if ([string]::IsNullOrEmpty($Target)) {
    $Target = (git branch --show-current).Trim()
}

if ($Target -eq $Baseline) {
    throw "Target branch '$Target' cannot be the same as baseline branch '$Baseline'."
}

$solutionPath = $PSScriptRoot
$project = Join-Path $solutionPath "test" "Benchmarks" "Benchmarks.csproj"
$artifacts = Join-Path $solutionPath "BenchmarkDotNet.Artifacts"

$branches = @($Target, $Baseline)

foreach ($branch in $branches) {

    Write-Information "Checking out branch '$branch'..."

    git checkout $branch

    if ($LASTEXITCODE -ne 0) {
      throw "git checkout $branch failed with exit code $LASTEXITCODE"
    }

    Write-Information "Running benchmarks for branch '$branch'..."

    $additionalArgs = @()

    $additionalArgs += "--artifacts"
    $additionalArgs += (Join-Path $artifacts $branch.Replace("/", "_"))

    $additionalArgs += "--runtimes"
    $additionalArgs += $Runtimes

    $additionalArgs += "--filter"
    $additionalArgs += $Benchmarks

    if (-Not [string]::IsNullOrEmpty($Job)) {
        $additionalArgs += "--job"
        $additionalArgs += $Job
    }

    if ($EnableMemoryDiagnoser) {
        $additionalArgs += "--memory"
    }

    if ($EnableEventPipeProfiler) {
        $additionalArgs += "--profiler"
        $additionalArgs += "EP"
    }

    $dotnetArgs = @(
        "run"
        "--configuration", $Configuration
        "--framework", $Framework
        "--project", $project
        "--"
    ) + $additionalArgs

    $p = Start-Process -FilePath "dotnet" -ArgumentList $dotnetArgs -NoNewWindow -Wait -PassThru

    if ($p.ExitCode -ne 0) {
        throw "Benchmarks failed with exit code $($p.ExitCode)."
    }
}

Write-Information "Finished running benchmarks."

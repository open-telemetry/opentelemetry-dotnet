#! /usr/bin/env pwsh

#Requires -PSEdition Core
#Requires -Version 7

<#
.SYNOPSIS
Runs BenchmarkDotNet benchmarks for a target git ref and an optional baseline ref.

.DESCRIPTION
Checks out each requested ref, runs the Benchmarks test project with the supplied
BenchmarkDotNet filters and options, and writes artifacts into ref-specific
subdirectories under BenchmarkDotNet.Artifacts in the root of the current repository.

This script requires a clean working tree because it switches refs while running.
When -Target is omitted, the current branch is used. In a detached HEAD state,
-Target must be explicitly provided.

.PARAMETER Benchmarks
One or more BenchmarkDotNet filter expressions to pass through using --filter.

.PARAMETER Target
The target branch, tag, or commit to benchmark. Defaults to the current branch.

.PARAMETER Baseline
The baseline branch, tag, or commit to benchmark for comparison. Defaults to "main".

.PARAMETER Job
The BenchmarkDotNet job to use (e.g. "Short"). Defaults to "Default".

.PARAMETER Runtimes
One or more target frameworks to benchmark. Defaults to "net10.0".

.PARAMETER EnableMemoryDiagnoser
Enables the BenchmarkDotNet memory diagnoser.

.PARAMETER EnableEventPipeProfiler
Enables the BenchmarkDotNet EventPipe profiler.

.PARAMETER SkipBaseline
Runs only the target benchmark and skips the baseline ref.

.EXAMPLE
./benchmark.ps1 @("*SamplerBenchmarks*") -SkipBaseline

Runs the matching benchmarks for the current branch only.

.EXAMPLE
./benchmark.ps1 @("*ExporterBenchmarks*") -Target my-feature -Job Short -Runtimes @("net10.0", "net462")

Runs the matching exporter benchmarks for the my-feature branch and main using the
"Short" job for .NET 10 and .NET Framework 4.6.2.
#>

param(
    [Parameter(Mandatory = $true, Position = 0)][string[]] $Benchmarks,
    [Parameter(Mandatory = $false)][string] $Target,
    [Parameter(Mandatory = $false)][string] $Baseline = "main",
    [Parameter(Mandatory = $false)][string] $Job = "Default",
    [Parameter(Mandatory = $false)][string[]] $Runtimes = @("net10.0"),
    [Parameter(Mandatory = $false)][switch] $EnableMemoryDiagnoser,
    [Parameter(Mandatory = $false)][switch] $EnableEventPipeProfiler,
    [Parameter(Mandatory = $false)][switch] $SkipBaseline
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$ProgressPreference = "SilentlyContinue"

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    & git @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-GitOutput {
    param(
        [Parameter(Mandatory = $true)][string[]] $Arguments
    )

    $output = & git @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }

    return ($output | Out-String).Trim()
}

function Resolve-GitCommit {
    param(
        [Parameter(Mandatory = $true)][string] $RefName
    )

    return Get-GitOutput -Arguments @("rev-parse", "--verify", "--end-of-options", "$RefName`^{commit}")
}

function ConvertTo-SafePathSegment {
    param(
        [Parameter(Mandatory = $true)][string] $Value
    )

    $invalidChars = [System.Collections.Generic.HashSet[char]]::new([System.IO.Path]::GetInvalidFileNameChars())
    $invalidChars.Add([System.IO.Path]::DirectorySeparatorChar) | Out-Null
    $invalidChars.Add([System.IO.Path]::AltDirectorySeparatorChar) | Out-Null

    $buffer = $Value.ToCharArray()

    for ($i = 0; $i -lt $buffer.Length; $i++) {
        if ($invalidChars.Contains($buffer[$i])) {
            $buffer[$i] = '_'
        }
    }

    $safeValue = [string]::new($buffer)

    $safeValue = $safeValue.TrimEnd('.')

    if ([string]::IsNullOrEmpty($safeValue)) {
        return "_"
    }

    return $safeValue
}

$Configuration = "Release"
$Framework = "net10.0"

if (-not ($Runtimes | Where-Object { $_ -notmatch "^net4\d+$" })) {
    $Framework = "net462"
}

if ($Benchmarks.Count -eq 0) {
    throw "At least one benchmark filter must be specified."
}

if ($Runtimes.Count -eq 0) {
    throw "At least one .NET runtime must be specified."
}

$unsupportedRuntimes = $Runtimes | Where-Object { $_ -match "^net4\d+$" }

if (-not $IsWindows -and $unsupportedRuntimes.Count -gt 0) {
    throw ".NET Framework runtimes ($($unsupportedRuntimes -join ", ")) are only supported on Windows."
}

$workingTreeStatus = Get-GitOutput -Arguments @("status", "--porcelain")

if (-not [string]::IsNullOrWhiteSpace($workingTreeStatus)) {
    throw "The git working tree must be clean before running benchmarks."
}

$startingBranch = Get-GitOutput -Arguments @("branch", "--show-current")
$startingCommit = Resolve-GitCommit -RefName "HEAD"
$targetName = $Target
$baselineName = $Baseline

if ($env:GITHUB_ACTIONS -eq "true") {
    if ([string]::IsNullOrEmpty($Target)) {
        $Target = $env:GITHUB_SHA

        if (-not [string]::IsNullOrEmpty($env:GITHUB_HEAD_REF)) {
            $targetName = $env:GITHUB_HEAD_REF
        }
        elseif (-not [string]::IsNullOrEmpty($env:GITHUB_REF_NAME)) {
            $targetName = $env:GITHUB_REF_NAME
        }
    }

    if (-not $PSBoundParameters.ContainsKey("Baseline")) {
        if (-not [string]::IsNullOrEmpty($env:GITHUB_BASE_REF)) {
            $Baseline = "origin/$($env:GITHUB_BASE_REF)"
            $baselineName = $env:GITHUB_BASE_REF
        }
        elseif ($env:GITHUB_REF_TYPE -eq "branch" -and $env:GITHUB_REF_NAME -like "main*") {
            $Baseline = "$($env:GITHUB_SHA)~1"
            $baselineName = "$($env:GITHUB_REF_NAME)~1"
        }
    }
}

if ([string]::IsNullOrEmpty($Target)) {
    if ([string]::IsNullOrEmpty($startingBranch)) {
        throw "Target must be specified when the repository is in a detached HEAD state."
    }

    $Target = $startingBranch
}

if ([string]::IsNullOrEmpty($targetName)) {
    $targetName = $Target
}

if ([string]::IsNullOrEmpty($baselineName)) {
    $baselineName = $Baseline
}

if (-not $SkipBaseline -and $Target -eq $Baseline) {
    throw "Target branch '$Target' cannot be the same as baseline branch '$Baseline'."
}

$solutionPath = $PSScriptRoot
$project = Join-Path $solutionPath "test" "Benchmarks" "Benchmarks.csproj"
$artifacts = Join-Path $solutionPath "BenchmarkDotNet.Artifacts"

$targetBenchmark = [PSCustomObject]@{
    Name = $targetName
    Commit = Resolve-GitCommit -RefName $Target
}

if ($SkipBaseline) {
    $benchmarkRefs = @($targetBenchmark)
}
else {
    $benchmarkRefs = @(
        $targetBenchmark,
        [PSCustomObject]@{
            Name = $baselineName
            Commit = Resolve-GitCommit -RefName $Baseline
        }
    )
}

try {
    foreach ($benchmarkRef in $benchmarkRefs) {
        $branch = $benchmarkRef.Name

        Write-Information "Checking out ref '$branch'..."

        Invoke-Git -Arguments @("switch", "--detach", "--", $benchmarkRef.Commit)

        Write-Information "Running benchmarks for ref '$branch'..."

        $additionalArgs = @()

        $additionalArgs += "--artifacts"
        $additionalArgs += (Join-Path $artifacts (ConvertTo-SafePathSegment -Value $branch))

        $additionalArgs += "--runtimes"
        $additionalArgs += $Runtimes

        $additionalArgs += "--filter"
        $additionalArgs += $Benchmarks

        if (-not [string]::IsNullOrEmpty($Job)) {
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
}
finally {
    if (-not [string]::IsNullOrEmpty($startingBranch)) {
        Write-Information "Restoring original ref '$startingBranch'..."
        Invoke-Git -Arguments @("switch", "--", $startingBranch)
    }
    else {
        Write-Information "Restoring original ref '$startingCommit'..."
        Invoke-Git -Arguments @("switch", "--detach", "--", $startingCommit)
    }
}

Write-Information "Finished running benchmarks."

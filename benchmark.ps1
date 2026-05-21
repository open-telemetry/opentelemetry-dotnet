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

if ($env:GITHUB_ACTIONS -eq "true") {
    $startingBranch = $env:GITHUB_REF_NAME

    if ($startingBranch -eq "main") {
        $Baseline = "$startingBranch~1"
    }
}

if ([string]::IsNullOrEmpty($Target)) {
    if ([string]::IsNullOrEmpty($startingBranch)) {
        throw "Target must be specified when the repository is in a detached HEAD state."
    }

    $Target = $startingBranch
}

if ($Target -eq $Baseline) {
    throw "Target branch '$Target' cannot be the same as baseline branch '$Baseline'."
}

$solutionPath = $PSScriptRoot
$project = Join-Path $solutionPath "test" "Benchmarks" "Benchmarks.csproj"
$artifacts = Join-Path $solutionPath "BenchmarkDotNet.Artifacts"

$benchmarkRefs = @(
    [PSCustomObject]@{
        Name = $Target
        Commit = Resolve-GitCommit -RefName $Target
    },
    [PSCustomObject]@{
        Name = $Baseline
        Commit = Resolve-GitCommit -RefName $Baseline
    }
)

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

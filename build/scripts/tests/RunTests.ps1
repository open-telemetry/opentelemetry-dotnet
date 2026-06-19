#! /usr/bin/env pwsh

#Requires -PSEdition Core
#Requires -Version 7

<#
.SYNOPSIS
Runs the Pester tests for the PowerShell scripts in build/scripts.

.DESCRIPTION
Installs the pinned version of Pester (if it is not already available),
then runs all of the *.Tests.ps1 files in this directory with code coverage
enabled. The same script is used both locally and in CI so that the tests
run in an identical and reproducible way on Windows, Linux and macOS.

.EXAMPLE
./build/scripts/tests/RunTests.ps1

Runs the tests using the default pinned version of Pester.
#>

[CmdletBinding()]
param(
    # renovate: datasource=nuget depName=Pester
    [string]$PesterVersion = "5.7.1",
    [int]$MinimumCoveragePercent = 80
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

$IsGitHubActions = -Not [string]::IsNullOrEmpty($env:GITHUB_ACTIONS)
$IsGitHubActionsDebug = -Not [string]::IsNullOrEmpty($env:ACTIONS_STEP_DEBUG)

# Ensure the pinned version of Pester is installed so that the tests behave
# the same way regardless of which version of Pester (if any) is already
# present on the machine running them.
$pester = Get-Module -Name "Pester" -ListAvailable | Where-Object { $_.Version -eq $PesterVersion }

if ($null -eq $pester) {
    Write-Output "Installing Pester $PesterVersion..."
    Install-Module `
        -Name "Pester" `
        -RequiredVersion $PesterVersion `
        -Scope CurrentUser `
        -Force `
        -SkipPublisherCheck
}

Remove-Module -Name "Pester" -Force -ErrorAction SilentlyContinue
Import-Module -Name "Pester" -RequiredVersion $PesterVersion -Force

$scriptsDirectory = Split-Path -Path $PSScriptRoot -Parent
$testsDirectory = $PSScriptRoot

# The scripts to measure code coverage for. Test scripts and the helper
# scripts that are only used for testing/CI are deliberately excluded.
$coveragePaths = @(
    "add-labels.psm1",
    "finalize-publicapi.ps1",
    "post-release.psm1",
    "prepare-release.psm1",
    "update-changelogs.ps1"
) | ForEach-Object { Join-Path -Path $scriptsDirectory -ChildPath $_ }

$configuration = New-PesterConfiguration
$configuration.Run.Path = $testsDirectory
$configuration.Run.Exit = $false
$configuration.Run.PassThru = $true
$configuration.CodeCoverage.Enabled = $true
$configuration.CodeCoverage.Path = $coveragePaths
$configuration.CodeCoverage.OutputPath = Join-Path -Path $testsDirectory -ChildPath "coverage.xml"
$configuration.CodeCoverage.OutputFormat = "JaCoCo"
$configuration.CodeCoverage.CoveragePercentTarget = $MinimumCoveragePercent
$configuration.TestResult.Enabled = $IsGitHubActions
$configuration.TestResult.OutputPath = Join-Path -Path $testsDirectory -ChildPath "testResults.xml"
$configuration.Output.Verbosity = $IsGitHubActionsDebug ? "Diagnostic" : "Detailed"

Write-Output "Running Pester tests..."

$result = Invoke-Pester -Configuration $configuration

$failed = $false

if ($result.Result -ne "Passed") {
    Write-Warning "One or more tests failed."
    $failed = $true
}

$coveragePercent = if ($null -ne $result.CodeCoverage) {
    [Math]::Round($result.CodeCoverage.CoveragePercent, 2)
}
else {
    0
}

if ($coveragePercent -lt $MinimumCoveragePercent) {
    Write-Warning "Code coverage of $coveragePercent% is below the required minimum of $MinimumCoveragePercent%."
    $failed = $true
}
else {
    Write-Output "Code coverage is $coveragePercent% (minimum required is $MinimumCoveragePercent%)."
}

if ($failed) {
    exit 1
}

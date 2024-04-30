param([string]$targetNetFramework)

$rootDirectory = Split-Path $PSScriptRoot -Parent
$publishOutput = dotnet publish $rootDirectory/test/OpenTelemetry.AotCompatibility.TestApp/OpenTelemetry.AotCompatibility.TestApp.csproj --framework $targetNetFramework -nodeReuse:false /p:UseSharedCompilation=false /p:ExposeExperimentalFeatures=true

$actualWarningCount = 0

foreach ($line in $($publishOutput -split "`r`n"))
{
    if (($line -like "*analysis warning IL*") -or ($line -like "*analysis error IL*"))
    {
        Write-Host $line
        $actualWarningCount += 1
    }
}

Write-Host "Actual warning count is:", $actualWarningCount
$expectedWarningCount = 0

if ($LastExitCode -ne 0)
{
    Write-Host "There was an error while publishing AotCompatibility Test App. LastExitCode is:", $LastExitCode
    Write-Host $publishOutput
}

$runtime = $IsWindows ? "win-x64" : ($IsMacOS ? "macos-x64" : "linux-x64")
$app = $IsWindows ? "./OpenTelemetry.AotCompatibility.TestApp.exe" : "./OpenTelemetry.AotCompatibility.TestApp"

Push-Location $rootDirectory/test/OpenTelemetry.AotCompatibility.TestApp/bin/Release/$targetNetFramework/$runtime

Write-Host "Executing test App..."
$app
Write-Host "Finished executing test App"

if ($LastExitCode -ne 0)
{
  Write-Host "There was an error while executing AotCompatibility Test App. LastExitCode is:", $LastExitCode
}

Pop-Location

$testPassed = 0
if ($actualWarningCount -ne $expectedWarningCount)
{
    $testPassed = 1
    Write-Host "Actual warning count:", actualWarningCount, "is not as expected. Expected warning count is:", $expectedWarningCount
}

Exit $testPassed

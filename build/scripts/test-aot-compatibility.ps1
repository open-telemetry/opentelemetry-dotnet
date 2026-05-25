param([string]$targetNetFramework)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$WarningPreference = "Continue"

$rootDirectory = Get-Location

$publishOutput = dotnet publish $rootDirectory/test/OpenTelemetry.AotCompatibility.TestApp/OpenTelemetry.AotCompatibility.TestApp.csproj --framework $targetNetFramework -nodeReuse:false /p:UseSharedCompilation=false /p:ExposeExperimentalFeatures=true

$actualWarningCount = 0
$testPassed = 0

foreach ($line in $($publishOutput -split "`r`n"))
{
    if (($line -like "*analysis warning IL*") -or ($line -like "*analysis error IL*"))
    {
        Write-Warning $line
        $actualWarningCount += 1
    }
}

Write-Information "Actual warning count is: $actualWarningCount"
$expectedWarningCount = 0

if ($LastExitCode -ne 0)
{
    $testPassed = 1
    Write-Warning "There was an error while publishing AotCompatibility Test App. LastExitCode is: $LastExitCode"
    Write-Warning $publishOutput
}

$app = $IsWindows ? "./OpenTelemetry.AotCompatibility.TestApp.exe" : "./OpenTelemetry.AotCompatibility.TestApp"

Push-Location $rootDirectory/artifacts/publish/OpenTelemetry.AotCompatibility.TestApp/release_$targetNetFramework

Write-Information "Executing test App..."
& $app
Write-Information "Finished executing test App"

if ($LastExitCode -ne 0)
{
    $testPassed = 1
    Write-Warning "There was an error while executing AotCompatibility Test App. LastExitCode is: $LastExitCode"
    Write-Warning $publishOutput
}

Pop-Location

if ($actualWarningCount -ne $expectedWarningCount)
{
    $testPassed = 1
    Write-Warning "Actual warning count: $actualWarningCount is not as expected. Expected warning count is: $expectedWarningCount"
}

Exit $testPassed

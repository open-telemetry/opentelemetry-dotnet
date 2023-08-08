$rootDirectory = Split-Path $PSScriptRoot -Parent
$publishOutput = dotnet publish $rootDirectory\test\OpenTelemetry.AotCompatibility.TestApp\OpenTelemetry.AotCompatibility.TestApp.csproj -nodeReuse:false /p:UseSharedCompilation=false

$actualWarningCount = 0

foreach ($line in $($publishOutput -split "`r`n"))
{
    if ($line -like "*analysis warning IL*") 
    {
        Write-Host $line
        $actualWarningCount += 1
    }
}

Write-Host "Actual warning count is:", $actualWarningCount
$expectedWarningCount = 28

$testPassed = 0
if ($actualWarningCount -ne $expectedWarningCount)
{
    $testPassed = 1
    Write-Host "Actual warning count:", actualWarningCount, "is not as expected. Expected warning count is:", $expectedWarningCount
}

Exit $testPassed

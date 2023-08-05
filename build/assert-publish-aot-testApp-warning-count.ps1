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


pushd $rootDirectory\test\OpenTelemetry.AotCompatibility.TestApp/bin/Debug/net7.0/linux-x64
Dir -Recurse . | Get-Childitem -Name

chmod +x ./OpenTelemetry.AotCompatibility.TestApp
chmod 777 ./OpenTelemetry.AotCompatibility.TestApp
Write-Host "Execute test App"
./OpenTelemetry.AotCompatibility.TestApp
Write-Host "Finished executing test App"
Write-Host "LastExitCode is:", $LastExitCode
Write-Host "Exit without error", $?
popd

Write-Host "Actual warning count is:", $actualWarningCount
$expectedWarningCount = 28

$testPassed = 0
if ($actualWarningCount -ne $expectedWarningCount)
{
    $testPassed = 1
    Write-Host "Actual warning count:", actualWarningCount, "is not as expected. Expected warning count is:", $expectedWarningCount
}

Exit $testPassed

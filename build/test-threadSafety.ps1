Write-Host "Test running coyote"

$rootDirectory = Split-Path $PSScriptRoot -Parent

Write-Host "Install Coyote CLI"
dotnet tool install --global Microsoft.Coyote.CLI

Write-Host "Build OpenTelemetry.Tests proj"
dotnet build $rootDirectory/test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj

Write-Host "Run Coyote rewrite"
coyote rewrite $rootDirectory/test/OpenTelemetry.Tests/coyote.json

Write-Host "Execute re-written binary"
$Output = dotnet test $rootDirectory/test/OpenTelemetry.Tests/bin/Debug/net8.0/OpenTelemetry.Tests.dll --filter MultithreadedLongHistogramTest_Coyote

Write-Host "Verify test pass"
foreach ($line in $($Output -split "`r`n"))
{
    Write-Host $line
    if ($line -contains "*pass*")
    {
        Write-Host "PASSED!"
    }
}

param(
  [Parameter(Mandatory=$true)][string]$coyoteVersion="",
  [Parameter(Mandatory=$true)][string]$targetFramework
)

Write-Host "Test running coyote"
$rootDirectory = Split-Path $PSScriptRoot -Parent

Write-Host "Install Coyote CLI."
dotnet tool install --global Microsoft.Coyote.CLI

Write-Host "Build OpenTelemetry.Tests project."
dotnet build $rootDirectory/test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj

$artifactsPath = Join-Path $rootDirectory "test/OpenTelemetry.Tests/bin/Debug/$targetNetFramework"

Write-Host "Generating Coyote rewriting options JSON file."
$dlls = Get-ChildItem $artifactsPath -Filter *.dll
$assemblies = New-Object System.Collections.ArrayList

foreach ($dll in $dlls)
{
    $assemblies.Add("Assemblies", $dll)
}

$RewriteOptionsJson = @{}
[void]$RewriteOptionsJson.Add("AssembliesPath", $artifactsPath)
[void]$RewriteOptionsJson.Add("Assemblies", $assemblies)
$RewriteOptionsJson | ConvertTo-Json -Compress | Set-Content "$rootDirectory/test/OpenTelemetry.Tests/rewrite.coyote.json"

Write-Host "Rewritten Json file:", $RewriteOptionsJson

Write-Host "Run Coyote rewrite."
coyote rewrite $rootDirectory/test/OpenTelemetry.Tests/rewrite.coyote.json
Write-Host "done re-written"

Write-Host "Execute re-written binary."
# test name can be passed in
$Output = dotnet test $artifactsPath/OpenTelemetry.Tests.dll --filter MultithreadedLongHistogramTest_Coyote

Write-Host "Verify test pass"
foreach ($line in $($Output -split "`r`n"))
{
    Write-Host $line
    if ($line -contains "*pass*")
    {
        Write-Host "PASSED!"
    }
}

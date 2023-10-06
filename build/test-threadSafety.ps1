param(
  [Parameter(Mandatory=$true)][string]$coyoteVersion="",
  [Parameter(Mandatory=$true)][string]$targetFramework
)

Write-Host "Test running Coyote."
$rootDirectory = Split-Path $PSScriptRoot -Parent

Write-Host "Install Coyote CLI."
dotnet tool install --global Microsoft.Coyote.CLI

Write-Host "Build OpenTelemetry.Tests project."
dotnet build $rootDirectory\test\OpenTelemetry.Tests\OpenTelemetry.Tests.csproj

$artifactsPath = Join-Path $rootDirectory "test\OpenTelemetry.Tests\bin\Debug\$targetFramework"

Write-Host "ArtifactsPath is:", $artifactsPath

Write-Host "Generating Coyote rewriting options JSON file."
$assemblies = Get-ChildItem $artifactsPath -Filter OpenTelemetry*.dll | ForEach-Object {$_.Name}

$RewriteOptionsJson = @{}
[void]$RewriteOptionsJson.Add("AssembliesPath", $artifactsPath)
[void]$RewriteOptionsJson.Add("Assemblies", $assemblies)
$RewriteOptionsJson | ConvertTo-Json -Compress | Set-Content "$rootDirectory\test\OpenTelemetry.Tests\rewrite.coyote.json"

Write-Host "Rewritten Json file:"
$obj = Get-Content -Path $rootDirectory\test\OpenTelemetry.Tests\rewrite.coyote.json -Raw | ConvertFrom-Json
Write-Host $obj

Write-Host "Run Coyote rewrite."
coyote rewrite $rootDirectory\test\OpenTelemetry.Tests\rewrite.coyote.json
Write-Host "Done re-written."

Write-Host "Execute re-written binary."
# test name can be passed in
$Output = dotnet test $artifactsPath\OpenTelemetry.Tests.dll --filter MultithreadedLongHistogramTest_Coyote

Write-Host "Verify test pass."
foreach ($line in $($Output -split "`r`n"))
{
    Write-Host $line
    if ($line -contains "*pass*")
    {
        Write-Host "PASSED!"
    }
}

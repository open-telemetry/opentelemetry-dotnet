param(
  [Parameter(Mandatory=$true)][string]$coyoteVersion="",
  [Parameter(Mandatory=$true)][string]$targetFramework,
  [Parameter(Mandatory=$true)][string]$methodNames
)

$rootDirectory = Split-Path $PSScriptRoot -Parent

Write-Host "Install Coyote CLI."
dotnet tool install --global Microsoft.Coyote.CLI

Write-Host "Build OpenTelemetry.Tests project."
dotnet build $rootDirectory\test\OpenTelemetry.Tests\OpenTelemetry.Tests.csproj

$artifactsPath = Join-Path $rootDirectory "test\OpenTelemetry.Tests\bin\Debug\$targetFramework"

Write-Host "Generate Coyote rewriting options JSON file."
$assemblies = Get-ChildItem $artifactsPath -Filter OpenTelemetry*.dll | ForEach-Object {$_.Name}

$RewriteOptionsJson = @{}
[void]$RewriteOptionsJson.Add("AssembliesPath", $artifactsPath)
[void]$RewriteOptionsJson.Add("Assemblies", $assemblies)
$RewriteOptionsJson | ConvertTo-Json -Compress | Set-Content "$rootDirectory\test\OpenTelemetry.Tests\rewrite.coyote.json"

Write-Host "Run Coyote rewrite."
coyote rewrite $rootDirectory\test\OpenTelemetry.Tests\rewrite.coyote.json

Write-Host "Execute re-written binary."
$Output = dotnet test $artifactsPath\OpenTelemetry.Tests.dll --filter $methodNames

Write-Host "Coyote test output:"
foreach ($line in $($Output -split "`r`n"))
{
    Write-Host $line
}

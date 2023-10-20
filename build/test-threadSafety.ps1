param(
  [Parameter()][string]$coyoteVersion="1.7.10",
  [Parameter(Mandatory=$true)][string]$testProjectName,
  [Parameter(Mandatory=$true)][string]$targetFramework,
  [Parameter()][string]$categoryName="CoyoteConcurrencyTests",
  [Parameter()][string]$configuration="Release"
)

$env:OTEL_RUN_COYOTE_TESTS = 'true'

$rootDirectory = Split-Path $PSScriptRoot -Parent

Write-Host "Install Coyote CLI."
dotnet tool install --global Microsoft.Coyote.CLI

Write-Host "Build $testProjectName project."
dotnet build $rootDirectory\test\$testProjectName\$testProjectName.csproj --configuration $configuration

$artifactsPath = Join-Path $rootDirectory "test\$testProjectName\bin\$configuration\$targetFramework"

Write-Host "Generate Coyote rewriting options JSON file."
$assemblies = Get-ChildItem $artifactsPath -Filter OpenTelemetry*.dll | ForEach-Object {$_.Name}

$RewriteOptionsJson = @{}
[void]$RewriteOptionsJson.Add("AssembliesPath", $artifactsPath)
[void]$RewriteOptionsJson.Add("Assemblies", $assemblies)
$RewriteOptionsJson | ConvertTo-Json -Compress | Set-Content "$rootDirectory\test\$testProjectName\rewrite.coyote.json"

Write-Host "Run Coyote rewrite."
coyote rewrite $rootDirectory\test\$testProjectName\rewrite.coyote.json

Write-Host "Execute re-written binary."
$Output = dotnet test $artifactsPath\$testProjectName.dll --framework $targetFramework --filter CategoryName=$categoryName

Write-Host "Coyote test output:"
foreach ($line in $($Output -split "`r`n"))
{
    Write-Host $line
}

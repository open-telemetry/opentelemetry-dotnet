param(
  [Parameter()][string]$coyoteVersion="1.7.11",
  [Parameter(Mandatory=$true)][string]$testProjectName,
  [Parameter(Mandatory=$true)][string]$targetFramework,
  [Parameter()][string]$categoryName="CoyoteConcurrencyTests",
  [Parameter()][string]$configuration="Release"
)

$ErrorActionPreference = "Stop"

$env:OTEL_RUN_COYOTE_TESTS = 'true'

$rootDirectory = Get-Location

Write-Host "Install Coyote CLI."
dotnet tool install --global Microsoft.Coyote.CLI

if ($LASTEXITCODE -ne 0) {
    throw "Microsoft.Coyote.CLI installation failed with exit code $LASTEXITCODE"
}

Write-Host "Build $testProjectName project."
dotnet build "$rootDirectory/test/$testProjectName/$testProjectName.csproj" --configuration $configuration

if ($LASTEXITCODE -ne 0) {
    throw "dotnet build failed with exit code $LASTEXITCODE"
}

$artifactsPath = Join-Path $rootDirectory "artifacts/bin/$testProjectName/$($configuration.ToLowerInvariant())_$targetFramework"

Write-Host "Generate Coyote rewriting options JSON file."
$assemblies = Get-ChildItem $artifactsPath -Filter OpenTelemetry*.dll | ForEach-Object {$_.Name}

$RewriteOptionsJson = @{}
[void]$RewriteOptionsJson.Add("AssembliesPath", $artifactsPath)
[void]$RewriteOptionsJson.Add("Assemblies", $assemblies)
$RewriteOptionsJson | ConvertTo-Json -Compress | Set-Content -Path "$rootDirectory/test/$testProjectName/rewrite.coyote.json"

Write-Host "Run Coyote rewrite."
coyote rewrite "$rootDirectory/test/$testProjectName/rewrite.coyote.json"

if ($LASTEXITCODE -ne 0) {
    throw "coyote rewrite failed with exit code $LASTEXITCODE"
}

Write-Host "Execute re-written binary."
dotnet test "$artifactsPath/$testProjectName.dll" --framework $targetFramework --filter CategoryName=$categoryName

if ($LASTEXITCODE -ne 0) {
    throw "dotnet test failed with exit code $LASTEXITCODE"
}

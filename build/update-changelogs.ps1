param(
  [Parameter(Mandatory=$true)][string]$minVerTagPrefix,
  [Parameter(Mandatory=$true)][string]$version
)

$projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$minVerTagPrefix</MinVerTagPrefix>" -List | Select Path | Split-Path -Parent

$content = "Unreleased

## $version

Released $(Get-Date -UFormat '%Y-%b-%d')"

foreach ($projectDir in $projectDirs) {
  $path = Join-Path -Path $projectDir -ChildPath "CHANGELOG.md"

  Write-Host "Updating $path"

  (Get-Content -Path $path) -replace "Unreleased", $content | Set-Content -Path $path
}

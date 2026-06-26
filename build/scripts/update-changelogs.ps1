param(
  [Parameter(Mandatory=$true)][string]$minVerTagPrefix,
  [Parameter(Mandatory=$true)][string]$version
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"

$projectDirs = Get-ChildItem -Path src/**/*.csproj | Select-String "<MinVerTagPrefix>$minVerTagPrefix</MinVerTagPrefix>" -List | Select-Object -ExpandProperty Path | Split-Path -Parent

# Format the release date using the invariant culture so the month abbreviation
# is always en-US (e.g. "Jun") regardless of the culture of the machine running
# the script.
$releaseDate = [System.DateTime]::Now.ToString('yyyy-MMM-dd', [System.Globalization.CultureInfo]::InvariantCulture)

$content = "Unreleased

## $version

Released $releaseDate"

foreach ($projectDir in $projectDirs) {
  $path = Join-Path -Path $projectDir -ChildPath "CHANGELOG.md"

  Write-Information "Updating $path"

  (Get-Content -Path $path) -replace "Unreleased", $content | Set-Content -Path $path
}

param([string]$package, [string]$version)

$workDir = "..\LastMajorVersionBinaries"
if (-Not (Test-Path $workDir))
{
    Write-Host "Working directory for previous package versions not found, creating..."
    New-Item -Path $workDir -ItemType "directory" | Out-Null
}

if (Test-Path -Path "$workDir\$package.$version.zip")
{
    Write-Debug "Previous package version already downloaded"
}
else
{
    Write-Host "Retrieving $package @$version for compatibility check"
    Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile "$workDir\$package.$version.zip"
}
if (Test-Path -Path "$workDir\$package\$version\lib")
{
    Write-Debug "Previous package version already extracted"
}
else
{
    Expand-Archive -LiteralPath "$workDir\$package.$version.zip" -DestinationPath "$workDir\$package\$version" -Force
}

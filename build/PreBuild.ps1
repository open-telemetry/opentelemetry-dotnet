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
    # because this script will be hit for each target framework in each project, there might be several copies running simultaneously
    # wait a random couple of seconds before continuing, checking again for the zip file
    Start-Sleep -Seconds (Get-Random -Minimum 0 -Maximum 3)
    if (Test-Path -Path "$workDir\$package.$version.zip")
    {
        Write-Host "Retrieving $package @$version for compatibility check"
        Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile "$workDir\$package.$version.zip"
    }
}
if (Test-Path -Path "$workDir\$package\$version")
{
    Write-Debug "Previous package version already extracted"
}
else
{
    Expand-Archive -LiteralPath "$workDir\$package.$version.zip" -DestinationPath "$workDir\$package\$version" -Force
}

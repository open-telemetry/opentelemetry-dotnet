param([string]$package, [string]$version)
$tempFile = "..\LastMajorVersionBinaries\$package.$version.txt"
if ((Test-Path -Path $tempFile) -OR (Test-Path -Path "..\LastMajorVersionBinaries\$package.$version.zip"))
{
    Write-Debug "Previous package version already exists or retrieval is in progress"
}
else
{
    New-Item $tempFile
    Write-Host "Retrieving $package @$version for compatibility check"

    Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile ..\LastMajorVersionBinaries\$package.$version.zip
    Expand-Archive -LiteralPath ..\LastMajorVersionBinaries\$package.$version.zip -DestinationPath ..\LastMajorVersionBinaries\$package\$version
    Remove-Item $tempFile
}

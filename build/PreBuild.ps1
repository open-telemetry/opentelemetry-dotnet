param([string]$package, [string]$version)

$tempFile = "..\LastMajorVersionBinaries\$package.$version.txt"
$retryCount = 0
while ((Test-Path -Path $tempFile) -and ($retryCount -Lt 10))
{
    Write-Host "Previous version retrieval in progress, waiting..."
    $retryCount++
    Start-Sleep -s 1
}
if (Test-Path -Path "..\LastMajorVersionBinaries\$package.$version.zip")
{
    Write-Debug "Previous package version already exists"
}
else
{
    New-Item $tempFile
    Write-Host "Retrieving $package @$version for compatibility check"

    Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile ..\LastMajorVersionBinaries\$package.$version.zip
    Expand-Archive -LiteralPath ..\LastMajorVersionBinaries\$package.$version.zip -DestinationPath ..\LastMajorVersionBinaries\$package\$version
}

if (Test-Path -Path $tempFile)
{
    Remove-Item $tempFile
}

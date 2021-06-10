param([string]$package, [string]$version)

$workDir = "..\LastMajorVersionBinaries"
if (-Not (Test-Path $workDir))
{
    Write-Host "Directory not found, creating..."
    New-Item -Path $workDir -ItemType "directory"
}

$lockFile = "$workDir\$package.$version.txt"
$retryCount = 0
while ((Test-Path -Path $lockFile) -and ($retryCount -Lt 10))
{
    Write-Host "Previous version retrieval in progress, waiting..."
    $retryCount++
    Start-Sleep -s 1
}
if (Test-Path -Path "$workDir\$package.$version.zip")
{
    Write-Debug "Previous package version already exists"
}
else
{
    New-Item -Path $lockFile
    Write-Host "Retrieving $package @$version for compatibility check"

    Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile "$workDir\$package.$version.zip"
    Expand-Archive -LiteralPath "$workDir\$package.$version.zip" -DestinationPath "$workDir\$package\$version"
}

if (Test-Path -Path $lockFile)
{
    Remove-Item -Path $lockFile
}

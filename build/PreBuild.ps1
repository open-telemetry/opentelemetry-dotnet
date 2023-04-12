param(
  [string]$package,
  [string]$version,
  [string]$workDir = ".\LastMajorVersionBinaries"
)

if (-Not (Test-Path $workDir))
{
    Write-Host "Working directory for compatibility check packages '$workDir' not found, creating..."
    New-Item -Path $workDir -ItemType "directory" | Out-Null
}

if (Test-Path -Path "$workDir\$package.$version.zip")
{
    Write-Host "Previous package $package@$version already downloaded for compatibility check"
}
else
{
    Write-Host "Retrieving package $package@$version for compatibility check"
    try
    {
        $Response = Invoke-WebRequest -Uri https://www.nuget.org/api/v2/package/$package/$version -Outfile "$workDir\$package.$version.zip"
    }
    catch
    {
        $StatusCode = $_.Exception.Response.StatusCode.value__
        throw "Error downloading the package $package@$version. Status code of the received response: $StatusCode"
    }
}

if (Test-Path -Path "$workDir\$package\$version\lib")
{
    Write-Host "Previous package $package@$version already extracted to '$workDir\$package\$version\lib'"
}
else
{
    Write-Host "Extracting package $package@$version from '$workDir\$package.$version.zip' to '$workDir\$package\$version' for compatibility check"
    try
    {
        Expand-Archive -LiteralPath "$workDir\$package.$version.zip" -DestinationPath "$workDir\$package\$version" -Force
    }
    catch
    {
        throw "Error extracting $package@$version.zip"
    }
}

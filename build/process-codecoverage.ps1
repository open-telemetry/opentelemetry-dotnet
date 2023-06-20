$rootDirectory = Split-Path $PSScriptRoot -Parent
[xml]$commonProps = Get-Content -Path $rootDirectory\Directory.Packages.props

$packages = $commonProps.Project.ItemGroup.PackageVersion
$microsoftCodeCoveragePkgVer = [string]($packages | Where-Object {$_.Include -eq "Microsoft.CodeCoverage"}).Version # This is collected in the format: "[17.4.1]"
$microsoftCodeCoveragePkgVer = $microsoftCodeCoveragePkgVer.Trim();
$microsoftCodeCoveragePkgVer = $microsoftCodeCoveragePkgVer.SubString(1, $microsoftCodeCoveragePkgVer.Length - 2) # Removing square brackets

$files = Get-ChildItem "TestResults" -Filter "*.coverage" -Recurse
Write-Host $env:USERPROFILE
foreach ($file in $files)
{
    $command = $env:USERPROFILE+ '\.nuget\packages\microsoft.codecoverage\' + $microsoftCodeCoveragePkgVer + '\build\netstandard2.0\CodeCoverage\CodeCoverage.exe analyze /output:' + $file.DirectoryName + '\' + $file.Name + '.xml '+ $file.FullName
    Write-Host $command
    Invoke-Expression $command
}

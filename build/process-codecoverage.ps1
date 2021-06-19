[xml]$commonProps = Get-Content -Path $PSScriptRoot\Common.props
$microsoftCodeCoveragePkgVer = [string]$commonProps.Project.PropertyGroup.MicrosoftCodeCoveragePkgVer # This is collected in the format: "[16.10.0]"
$microsoftCodeCoveragePkgVer = $microsoftCodeCoveragePkgVer.Trim();
$microsoftCodeCoveragePkgVer = $microsoftCodeCoveragePkgVer.SubString(1, $microsoftCodeCoveragePkgVer.Length - 2) # Removing square brackets
$files = Get-ChildItem "TestResults" -Filter "*.coverage" -Recurse
Write-Host $env:USERPROFILE
foreach ($file in $files)
{
    $command = $env:USERPROFILE+ '\.nuget\packages\microsoft.codecoverage\' + $microsoftCodeCoveragePkgVer + '\build\netstandard1.0\CodeCoverage\CodeCoverage.exe analyze /output:' + $file.DirectoryName + '\' + $file.Name + '.xml '+ $file.FullName
    Write-Host $command
    Invoke-Expression $command
}
